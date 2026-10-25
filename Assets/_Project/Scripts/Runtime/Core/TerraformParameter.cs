namespace Planetcraft.Core
{
    public enum TerraformParameter
    {
        Oxygen = 0,
        Heat = 1,
        Pressure = 2,
        Biomass = 3,
    }

    public static class TerraformParameters
    {
        public const int Count = 4;

        public static string DisplayName(TerraformParameter p) => p switch
        {
            TerraformParameter.Oxygen => "Oxigênio",
            TerraformParameter.Heat => "Calor",
            TerraformParameter.Pressure => "Pressão",
            TerraformParameter.Biomass => "Biomassa",
            _ => p.ToString(),
        };

        public static string Unit(TerraformParameter p) => p switch
        {
            TerraformParameter.Oxygen => "ppq",
            TerraformParameter.Heat => "pK",
            TerraformParameter.Pressure => "nPa",
            TerraformParameter.Biomass => "g",
            _ => "",
        };
    }

    [System.Serializable]
    public struct TerraformRate
    {
        public TerraformParameter parameter;
        public double perSecond;

        public TerraformRate(TerraformParameter parameter, double perSecond)
        {
            this.parameter = parameter;
            this.perSecond = perSecond;
        }
    }
}
