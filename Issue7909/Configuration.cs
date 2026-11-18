namespace Issue7909
{
    internal class Configuration
    {
        public string AmsNetID { get; set; }

        public int Port { get; set; }

        public int UnderSampling { get; set; } = 1000;

        public Variable Variable { get; set; } = new Variable();
    }

    internal class Variable
    {
        public string Name { get; set; }

        public int Size { get; set; } = 1;

        public AccessType AccessType { get; set; } = AccessType.Name;
    }

    internal enum AccessType
    {
        IndexGroupIndexOffset,
        Name,
    }
}