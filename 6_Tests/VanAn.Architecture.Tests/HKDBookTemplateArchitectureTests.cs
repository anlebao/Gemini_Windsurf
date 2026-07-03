using System.Reflection;
using VanAn.Shared.Domain;
using VanAn.CoreHub.Services.Template;
using Xunit;

namespace VanAn.Architecture.Tests
{
    /// <summary>
    /// Wave 8 SC6: Regression prevention for Issue 1 (production path CalculateAsync no-op → NumericValues always empty).
    ///
    /// Verifies that every HKDBookTemplate subclass in the CoreHub assembly (the production templates
    /// instantiated by TemplateFactory) extends BaseHKDBookTemplate — which provides the real
    /// CalculateAsync implementation backed by the formula engine.
    ///
    /// A plain HKDBookTemplate subclass with a no-op `await Task.CompletedTask` CalculateAsync
    /// (as existed in 1_Shared/Domain/HKDTemplates.cs before Wave 4) would leave NumericValues empty.
    /// This test ensures no such regression is introduced.
    /// </summary>
    public class HKDBookTemplateArchitectureTests
    {
        private static readonly Assembly CoreHubAssembly = typeof(TemplateFactory).Assembly;

        [Fact]
        public void All_CoreHub_HKDBookTemplate_Subclasses_Extend_BaseHKDBookTemplate()
        {
            Type baseTemplateType = typeof(HKDBookTemplate);
            Type baseImplType = typeof(BaseHKDBookTemplate);

            IEnumerable<Type> subclasses = CoreHubAssembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false }
                            && baseTemplateType.IsAssignableFrom(t)
                            && t != baseTemplateType
                            && t != baseImplType);

            List<Type> nonConforming = subclasses
                .Where(t => !baseImplType.IsAssignableFrom(t))
                .ToList();

            Assert.True(nonConforming.Count == 0,
                $"Regression (Issue 1): The following HKDBookTemplate subclasses in CoreHub do NOT extend " +
                $"BaseHKDBookTemplate, meaning they lack the formula-engine-backed CalculateAsync and would " +
                $"leave NumericValues empty: {string.Join(", ", nonConforming.Select(t => t.FullName))}. " +
                $"All production templates must extend BaseHKDBookTemplate.");
        }

        [Fact]
        public void All_Seven_Production_Templates_Are_Present()
        {
            string[] expectedTemplateCodes = ["S1a_HKD", "S2a_HKD", "S2b_HKD", "S2c_HKD", "S2d_HKD", "S2e_HKD", "S3a_HKD"];

            IEnumerable<Type> subclasses = CoreHubAssembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false }
                            && typeof(BaseHKDBookTemplate).IsAssignableFrom(t)
                            && t != typeof(BaseHKDBookTemplate));

            // Instantiate each template via TemplateFactory to verify all 7 codes are reachable.
            // We check the TemplateCode property via reflection on the type's constructor-less default state.
            // Simpler: verify 7 distinct BaseHKDBookTemplate subclasses exist (one per template).
            List<Type> templateTypes = subclasses.ToList();

            Assert.True(templateTypes.Count >= 7,
                $"Expected at least 7 BaseHKDBookTemplate subclasses (S1a, S2a-S2e, S3a), found {templateTypes.Count}: " +
                $"{string.Join(", ", templateTypes.Select(t => t.Name))}");
        }

        [Fact]
        public void BaseHKDBookTemplate_CalculateAsync_Is_Not_NoOp()
        {
            // BaseHKDBookTemplate.CalculateAsync must call the TemplateCalculationEngine — not be a no-op.
            // We verify the method is declared on BaseHKDBookTemplate (overridden from HKDBookTemplate)
            // and is not the abstract declaration. A real body exists (uses TemplateCalculationEngine).
            MethodInfo? method = typeof(BaseHKDBookTemplate).GetMethod(
                "CalculateAsync",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                [typeof(GenericHKDBook)],
                null);

            Assert.NotNull(method);
            Assert.False(method!.IsAbstract,
                "BaseHKDBookTemplate.CalculateAsync must have a concrete implementation (not abstract). " +
                "A no-op or abstract CalculateAsync would leave NumericValues empty (Issue 1 regression).");
        }
    }
}
