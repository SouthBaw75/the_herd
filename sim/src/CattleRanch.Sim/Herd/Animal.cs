namespace CattleRanch.Sim
{
    /// <summary>
    /// An individual head of cattle. Identity fields (Id, Sex, Breed, Genome) are
    /// fixed at birth; the sim-mutable fields (age, weight, health, pregnancy)
    /// carry internal setters so only sim code changes them.
    /// </summary>
    public sealed class Animal
    {
        /// <summary>Parameterless constructor for serialization (Newtonsoft-friendly).</summary>
        public Animal()
        {
            Id = string.Empty;
            Name = string.Empty;
            Breed = string.Empty;
            Health = HealthState.Healthy;
            Pregnancy = PregnancyState.None;
        }

        public Animal(
            string id,
            string name,
            Sex sex,
            string breed,
            int ageDays,
            double weightKg,
            Genome genome)
        {
            Id = id;
            Name = name;
            Sex = sex;
            Breed = breed;
            AgeDays = ageDays;
            WeightKg = weightKg;
            Genome = genome;
            Health = HealthState.Healthy;
            Pregnancy = PregnancyState.None;
        }

        public string Id { get; internal set; }
        public string Name { get; set; }
        public Sex Sex { get; internal set; }
        public string Breed { get; internal set; }
        public int AgeDays { get; internal set; }
        public double WeightKg { get; internal set; }
        public Genome Genome { get; internal set; }
        public HealthState Health { get; internal set; }
        public PregnancyState Pregnancy { get; internal set; }
    }
}
