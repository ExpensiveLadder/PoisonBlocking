using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using DynamicData;
using Mutagen.Bethesda.Plugins;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mutagen.Bethesda.WPF.Reflection.Attributes;

namespace PoisonBlocking
{
    public class TestSettings
    {
        [SettingName("Blocking Blocks Poisons and Diseases")]
        public bool BlockPoisons = true;

        [SettingName("Wards Block Poisons and Diseases")]
        public bool WardBlockPoisons = true;

        [SettingName("Blocking Blocks Enchantments")]
        public bool BlockEnchantments = true;

        [SettingName("Wards Block Enchantments")]
        public bool WardBlockEnchantments = true;

        [SettingName("Blacklisted FormKeys")]
        public List<string> blacklist = new()
        {
            "001852:ccBGSSSE037-Curious.esl",
            "10C645:Skyrim.esm",
            "017331:Skyrim.esm"
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

            List<ConditionFloat> blockConditions = new()
            {
                new ConditionFloat()
                {
                    Flags = Condition.Flag.OR,
                    Data = new FunctionConditionData()
                    {
                        Function = Condition.Function.IsBlocking,
                        RunOnType = Condition.RunOnType.Target
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
                        Function = Condition.Function.HasKeyword,
                        ParameterOneRecord = FormKey.Factory("01EA69:Skyrim.esm").ToLink<IKeywordGetter>(),
                        RunOnType = Condition.RunOnType.Target
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


            if (Settings.Value.BlockPoisons || Settings.Value.WardBlockPoisons)
            {
                foreach (var spellGetter in state.LoadOrder.PriorityOrder.Spell().WinningOverrides())
                {
                    if (spellGetter.EditorID != null && !spellGetter.EditorID.Contains("Trap") && spellGetter.TargetType == TargetType.Touch && (spellGetter.Type == SpellType.Poison || spellGetter.Type == SpellType.Disease) && !Settings.Value.blacklist.Contains(spellGetter.FormKey.ToString()))
                    {
                        Console.WriteLine(spellGetter.EditorID);
                        var spell = spellGetter.DeepCopy();

                        foreach (var effect in spell.Effects)
                        {
                            if (Settings.Value.BlockPoisons)
                            {
                                effect.Conditions.Add(blockConditions);
                            }
                            if (Settings.Value.WardBlockPoisons)
                            {
                                effect.Conditions.Add(wardConditions);
                            }
                        }

                        state.PatchMod.Spells.Add(spell);
                    }
                }

                foreach (var magiceffectGetter in state.LoadOrder.PriorityOrder.MagicEffect().WinningOverrides())
                {
                    if (magiceffectGetter.EditorID != null && !magiceffectGetter.EditorID.Contains("Trap") && magiceffectGetter.Keywords != null && magiceffectGetter.Keywords.Contains(magicAlchHarmful) && !Settings.Value.blacklist.Contains(magiceffectGetter.FormKey.ToString()))
                    {
                        Console.WriteLine(magiceffectGetter.EditorID);
                        var effect = magiceffectGetter.DeepCopy();

                        if (Settings.Value.BlockPoisons)
                        {
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
                foreach (var enchantmentGetter in state.LoadOrder.PriorityOrder.ObjectEffect().WinningOverrides())
                {
                    if (enchantmentGetter.EditorID != null && !enchantmentGetter.EditorID.Contains("Trap") && enchantmentGetter.CastType == CastType.FireAndForget && enchantmentGetter.TargetType == TargetType.Touch && enchantmentGetter.EnchantType == ObjectEffect.EnchantTypeEnum.Enchantment && !Settings.Value.blacklist.Contains(enchantmentGetter.FormKey.ToString()))
                    {
                        Console.WriteLine(enchantmentGetter.EditorID);
                        var enchantment = enchantmentGetter.DeepCopy();

                        foreach (var effect in enchantment.Effects)
                        {
                            if (Settings.Value.BlockEnchantments)
                            {
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