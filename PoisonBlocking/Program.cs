using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using DynamicData;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.WPF.Reflection.Attributes;
using Noggog;

namespace PoisonBlocking
{
    public class TestSettings
    {
        [SettingName("Blocking Blocks Poisons")]
        public bool BlockPoisons = true;

        [SettingName("Blocking Poison Requires Shield")]
        public bool ShieldPoisons = false;

        [SettingName("Blocking Blocks Diseases")]
        public bool BlockDiseases = true;

        [SettingName("Blocking Diseases Requires Shield")]
        public bool ShieldDiseases = false;

        [SettingName("Blocking Blocks Enchantments")]
        public bool BlockEnchantments = true;

        [SettingName("Blocking Enchantments Requires Shield")]
        public bool ShieldEnchantments = false;

        [SettingName("Wards Block Poisons")]
        public bool WardBlockPoisons = false;

        [SettingName("Wards Blocks Diseases")]
        public bool WardBlockDiseases = true;

        [SettingName("Wards Block Enchantments")]
        public bool WardBlockEnchantments = false;

        [SettingName("Blacklisted FormKeys")]
        public List<string> blacklist = new()
        {
            "001852:ccBGSSSE037-Curious.esl",
            "10C645:Skyrim.esm",
            "017331:Skyrim.esm",
            "0C5BE0:Skyrim.esm",
            "0C5BE1:Skyrim.esm",
            "03D37B:Dragonborn.esm",
            "069CE6:Skyrim.esm",
            "101BDF:Skyrim.esm",
            "016695:Dawnguard.esm",
            "014556:Dawnguard.esm"
        };
    }

    public class Program
    {
        static Lazy<TestSettings> Settings = null!;

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetAutogeneratedSettings(
                    nickname: "Settings",
                    path: "settings.json",
                    out Settings)
                .SetTypicalOpen(GameRelease.SkyrimSE, "YourPatcher.esp")
                .Run(args);
        }


        public static readonly FormLink<IKeywordGetter> magicAlchHarmful = FormKey.Factory("042509:Skyrim.esm").ToLink<IKeywordGetter>();
        public static readonly FormLink<IKeywordGetter> armorShield = FormKey.Factory("0965B2:Skyrim.esm").ToLink<IKeywordGetter>();

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            //Your code here!

            float CombatHitConeAngleValue;
            if (state.LinkCache.TryResolve<IGameSettingFloatGetter>("fCombatHitConeAngle", out var fCombatHitConeAngle))
            {
                if (fCombatHitConeAngle.Data == null) throw new Exception();
                CombatHitConeAngleValue = fCombatHitConeAngle.Data.Value;
            } else
            {
                CombatHitConeAngleValue = 35;
            }

            ConditionFloat shieldCondition = new()
            {
                Flags = Condition.Flag.OR,
                Data = new FunctionConditionData()
                {
                    Function = Condition.Function.WornHasKeyword,
                    ParameterOneNumber = 0,
                    ParameterOneRecord = armorShield,
                    RunOnType = Condition.RunOnType.Subject
                }
            };

            List<ConditionFloat> blockConditions = new()
            {
                new ConditionFloat()
                {
                    Flags = Condition.Flag.OR,
                    Data = new FunctionConditionData()
                    {
                        Function = Condition.Function.IsBlocking,
                        RunOnType = Condition.RunOnType.Subject
                    }
                },
                new ConditionFloat()
                {
                    CompareOperator = CompareOperator.LessThanOrEqualTo,
                    ComparisonValue = 180 - CombatHitConeAngleValue,
                    Flags = Condition.Flag.OR,
                    Data = new FunctionConditionData()
                    {
                        Function = Condition.Function.GetRelativeAngle,
                        ParameterTwoNumber = 90,
                        RunOnType = Condition.RunOnType.Subject
                    }
                },
                new ConditionFloat()
                {
                    CompareOperator = CompareOperator.GreaterThanOrEqualTo,
                    ComparisonValue = 180 + CombatHitConeAngleValue,
                    Data = new FunctionConditionData()
                    {
                        Function = Condition.Function.GetRelativeAngle,
                        ParameterTwoNumber = 90,
                        RunOnType = Condition.RunOnType.Subject
                    }
                }
            };

            List<ConditionFloat> wardConditions = new()
            {
                new ConditionFloat()
                {
                    Flags = Condition.Flag.OR,
                    Data = new FunctionConditionData()
                    {
                        Function = Condition.Function.HasMagicEffectKeyword,
                        ParameterOneRecord = FormKey.Factory("01EA69:Skyrim.esm").ToLink<IKeywordGetter>(),
                        RunOnType = Condition.RunOnType.Subject
                    }
                },
                new ConditionFloat()
                {
                    CompareOperator = CompareOperator.LessThanOrEqualTo,
                    ComparisonValue = 90,
                    Flags = Condition.Flag.OR,
                    Data = new FunctionConditionData()
                    {
                        Function = Condition.Function.GetRelativeAngle,
                        ParameterTwoNumber = 90,
                        RunOnType = Condition.RunOnType.Subject
                    }
                },
                new ConditionFloat()
                {
                    CompareOperator = CompareOperator.GreaterThanOrEqualTo,
                    ComparisonValue = 270,
                    Data = new FunctionConditionData()
                    {
                        Function = Condition.Function.GetRelativeAngle,
                        ParameterTwoNumber = 90,
                        RunOnType = Condition.RunOnType.Subject
                    }
                }
            };

            if (Settings.Value.BlockPoisons || Settings.Value.WardBlockPoisons || Settings.Value.BlockDiseases || Settings.Value.WardBlockDiseases)
            {
                foreach (var spellGetter in state.LoadOrder.PriorityOrder.Spell().WinningOverrides())
                {
                    if (spellGetter.TargetType == TargetType.Touch && spellGetter.EditorID != null && !spellGetter.EditorID.Contains("Trap") && !Settings.Value.blacklist.Contains(spellGetter.FormKey.ToString()))
                    {
                        if (spellGetter.Type == SpellType.Poison && (Settings.Value.WardBlockPoisons || Settings.Value.BlockPoisons))
                        {
                            Console.WriteLine(spellGetter.EditorID);
                            var spell = spellGetter.DeepCopy();
                            foreach (var effect in spell.Effects)
                            {
                                if (Settings.Value.BlockPoisons)
                                {
                                    if (Settings.Value.ShieldPoisons) effect.Conditions.Add(shieldCondition);
                                    effect.Conditions.Add(blockConditions);
                                }
                                if (Settings.Value.WardBlockPoisons)
                                {
                                    effect.Conditions.Add(wardConditions);
                                }
                            }
                            state.PatchMod.Spells.Add(spell);

                        } else if (spellGetter.Type == SpellType.Disease && (Settings.Value.WardBlockDiseases || Settings.Value.BlockDiseases))
                        {
                            Console.WriteLine(spellGetter.EditorID);
                            var spell = spellGetter.DeepCopy();
                            foreach (var effect in spell.Effects)
                            {
                                if (Settings.Value.BlockDiseases)
                                {
                                    if (Settings.Value.ShieldDiseases) effect.Conditions.Add(shieldCondition);
                                    effect.Conditions.Add(blockConditions);
                                }
                                if (Settings.Value.WardBlockDiseases)
                                {
                                    effect.Conditions.Add(wardConditions);
                                }
                            }
                            state.PatchMod.Spells.Add(spell);
                        }
                    }
                }
            }

            if (Settings.Value.BlockPoisons || Settings.Value.WardBlockPoisons)
            {
                foreach (var magiceffectGetter in state.LoadOrder.PriorityOrder.MagicEffect().WinningOverrides())
                {
                    if (magiceffectGetter.EditorID != null && !magiceffectGetter.EditorID.Contains("Trap") && magiceffectGetter.Keywords != null && magiceffectGetter.Keywords.Contains(magicAlchHarmful) && !Settings.Value.blacklist.Contains(magiceffectGetter.FormKey.ToString()))
                    {
                        Console.WriteLine(magiceffectGetter.EditorID);
                        var effect = magiceffectGetter.DeepCopy();

                        if (Settings.Value.BlockPoisons)
                        {
                            if (Settings.Value.ShieldPoisons) effect.Conditions.Add(shieldCondition);
                            effect.Conditions.Add(blockConditions);
                        }
                        if (Settings.Value.WardBlockPoisons)
                        {
                            effect.Conditions.Add(wardConditions);
                        }

                        state.PatchMod.MagicEffects.Add(effect);
                    }
                }
            }

            if (Settings.Value.BlockEnchantments || Settings.Value.WardBlockEnchantments)
            {
                List<IFormLinkNullableGetter<IEffectRecordGetter>> enchantments = new();
                foreach (var weaponGetter in state.LoadOrder.PriorityOrder.Weapon().WinningOverrides())
                {
                    if (weaponGetter.ObjectEffect != null && !enchantments.Contains(weaponGetter.ObjectEffect))
                    {
                        if (weaponGetter.ObjectEffect.TryResolve<IObjectEffectGetter>(state.LinkCache, out var enchantmentGetter))
                        {
                            if (!Settings.Value.blacklist.Contains(enchantmentGetter.FormKey.ToString()) && enchantmentGetter.TargetType == TargetType.Touch && enchantmentGetter.EnchantType == ObjectEffect.EnchantTypeEnum.Enchantment)
                            {
                                Console.WriteLine(enchantmentGetter.EditorID);
                                enchantments.Add(weaponGetter.ObjectEffect);
                                var enchantment = enchantmentGetter.DeepCopy();

                                foreach (var effect in enchantment.Effects)
                                {
                                    if (Settings.Value.blacklist.Contains(effect.BaseEffect.FormKey.ToString())) continue;
                                    if (Settings.Value.BlockEnchantments)
                                    {
                                        if (Settings.Value.ShieldEnchantments) effect.Conditions.Add(shieldCondition);
                                        effect.Conditions.Add(blockConditions);
                                    }
                                    if (Settings.Value.WardBlockEnchantments)
                                    {
                                        effect.Conditions.Add(wardConditions);
                                    }
                                }

                                state.PatchMod.ObjectEffects.Add(enchantment);
                            }

                        }
                    }
                }
            }
        }
    }
}
