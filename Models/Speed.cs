namespace NetLamer.Models
{
    public class Speed
    {
        public string Name { get; set; }
        public string SpeedForQoS { get; set; }

        public Speed(string name, string speedForQoS)
        {
            Name = name;
            SpeedForQoS = speedForQoS;
        }
    }
}