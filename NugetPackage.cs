using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NugetAcknowledgementExporter
{
    public class NugetPackageEqualityComparer : IEqualityComparer<NugetPackage>
    {
        public bool Equals([AllowNull] NugetPackage x, [AllowNull] NugetPackage y)
        {
            return x.Name == y.Name;
        }

        public int GetHashCode([DisallowNull] NugetPackage obj)
        {
            return obj.Name.GetHashCode();
        }
    }

    public class NugetPackage
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string LicenseUrl { get; set; }
        public string License { get; set; }
        public string Authors { get; set; }
        public string ProjectUrl { get; set; }

        public override string ToString()
        {
            return $"{Name} {Version}";
        }
    }
}
