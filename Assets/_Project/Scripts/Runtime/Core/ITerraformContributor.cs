namespace Planetcraft.Core
{
    public interface ITerraformContributor
    {
        // Soma as taxas por segundo deste contribuidor em `rates` (indexado por TerraformParameter).
        void AccumulateRates(double[] rates);
    }
}
