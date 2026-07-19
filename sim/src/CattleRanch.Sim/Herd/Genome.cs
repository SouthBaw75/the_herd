namespace CattleRanch.Sim
{
    /// <summary>
    /// Heritable traits, each on a 0..100 scale. Stored as <see cref="float"/>
    /// per the determinism rules (genome trait storage is float; all sim math is
    /// double). Breeding (later phase) blends parent genomes with variance.
    /// </summary>
    public struct Genome
    {
        public float GrowthRate;
        public float CalvingEase;
        public float Marbling;
        public float Mothering;
        public float HeatTolerance;
        public float DiseaseResistance;
        public float Fertility;

        public Genome(
            float growthRate,
            float calvingEase,
            float marbling,
            float mothering,
            float heatTolerance,
            float diseaseResistance,
            float fertility)
        {
            GrowthRate = growthRate;
            CalvingEase = calvingEase;
            Marbling = marbling;
            Mothering = mothering;
            HeatTolerance = heatTolerance;
            DiseaseResistance = diseaseResistance;
            Fertility = fertility;
        }
    }
}
