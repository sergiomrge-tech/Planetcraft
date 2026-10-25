namespace Planetcraft.Core
{
    public enum TerraformParameter
    {
        Oxygen = 0,
        Heat = 1,
        Pressure = 2,
        Biomass = 3,
        // Radiação removida do ar e do solo (a Última Guerra deixou a Terra contaminada).
        Decontamination = 4,
    }

    public static class TerraformParameters
    {
        public const int Count = 5;

        public static string DisplayName(TerraformParameter p) => p switch
        {
            TerraformParameter.Oxygen => "Oxigênio",
            TerraformParameter.Heat => "Calor",
            TerraformParameter.Pressure => "Pressão",
            TerraformParameter.Biomass => "Biomassa",
            TerraformParameter.Decontamination => "Descontaminação",
            _ => p.ToString(),
        };

        public static string Unit(TerraformParameter p) => p switch
        {
            TerraformParameter.Oxygen => "ppq",
            TerraformParameter.Heat => "pK",
            TerraformParameter.Pressure => "nPa",
            TerraformParameter.Biomass => "g",
            TerraformParameter.Decontamination => "pGy",
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
