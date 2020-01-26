namespace NugetAcknowledgementExporter
{
    public class Filter
    {
        public string NameStartsWith { get; set; }
        public string NameEquals { get; set; }
        public string NameContains { get; set; }

        public bool Matches(NugetPackage package)
        {
            var name = package.Name.ToLowerInvariant();

            if (NameEquals != null)
            {
                return name == NameEquals.ToLowerInvariant();
            }

            if (NameStartsWith != null)
            {
                return name.StartsWith(NameStartsWith.ToLowerInvariant());
            }

            if (NameContains != null)
            {
                return name.Contains(NameContains.ToLowerInvariant());
            }

            return false;
        }
    }
}
