using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NugetAcknowledgementExporter
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length == 0 || !Directory.Exists(args[0]))
            {
                Console.WriteLine("Please specify project directory");
                return;
            }

            var projects = FindAllCSProjs(args[0]);
            var includedPackages = projects
                .SelectMany(path => GetIncludedPackages(path))
                .Distinct(new NugetPackageEqualityComparer())
                .OrderBy(x => x.Name)
                .ToList();

            Console.WriteLine($"Detected {includedPackages.Count} nuget packages");

            Console.WriteLine("Downloading licenses...");
            await FillPackagesDetails(includedPackages);

            Console.WriteLine("Exporting project_packages.json...");
            ExportToJson(includedPackages);

            Console.WriteLine("Exporting acknowledgements.txt...");
            ExportAcknowledgements(includedPackages);

            Console.WriteLine("Finished");
        }

        static string GetNugetCachePath()
        {
            var nugetLists = "nuget locals all -list".Bash();
            var globalPackages = nugetLists.Split('\n').FirstOrDefault(x => x.StartsWith("global-packages: "));

            if (string.IsNullOrWhiteSpace(globalPackages)) return null;

            var path = globalPackages.Substring("global-packages: ".Length).Trim();
            return path;
        }

        static List<string> FindAllCSProjs(string basePath)
        {
            var result = new List<string>();

            result.AddRange(Directory.EnumerateFiles(basePath).Where(x => Path.GetExtension(x).ToLower() == ".csproj"));

            foreach (var directory in Directory.GetDirectories(basePath))
            {
                result.AddRange(FindAllCSProjs(directory));
            }

            return result;
        }

        static List<NugetPackage> GetIncludedPackages(string csproj)
        {
            var regex = new Regex("PackageReference Include=\"([^\"]+)\" Version=\"([^\"]+)\" \\/>");
            var regex2 = new Regex("PackageReference Include=\"([^\"]+)\">[\\s]+<Version>([^\\<]+)<\\/Version>");
            var file = File.ReadAllText(csproj);

            var packages = regex.Matches(file).Select(x => new NugetPackage
            {
                Name = x.Groups[1].Value,
                Version = x.Groups[2].Value
            }).ToList();

            packages.AddRange(regex2.Matches(file).Select(x => new NugetPackage
            {
                Name = x.Groups[1].Value,
                Version = x.Groups[2].Value
            }));

            return packages;
        }

        static async Task FillPackagesDetails(List<NugetPackage> packages)
        {
            var nugetCache = GetNugetCachePath();

            foreach (var package in packages)
            {
                var fileName = $"{package.Name.ToLower()}.nuspec";
                var specFilePath = Path.Combine(nugetCache, package.Name.ToLower(), package.Version, fileName);

                var spec = new XmlDocument();
                spec.Load(specFilePath);
                var metadata = spec.DocumentElement["metadata"];

                package.Authors = metadata["authors"].InnerText;
                package.ProjectUrl = metadata["projectUrl"].InnerText;
                package.LicenseUrl = metadata["licenseUrl"].InnerText;

                using var httpClient = new HttpClient();

                var headRequest = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, package.LicenseUrl));
                package.LicenseUrl = headRequest.RequestMessage.RequestUri.AbsoluteUri;

                headRequest = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, package.ProjectUrl));
                package.ProjectUrl = headRequest.RequestMessage.RequestUri.AbsoluteUri;

                var lowerLicenseUrl = package.LicenseUrl.ToLowerInvariant();
                if (lowerLicenseUrl.Contains("licenses.nuget.org/mit"))
                {
                    package.License = MITLicense(package.Authors);
                }
                else if (!lowerLicenseUrl.Contains("github.com") &&
                    !lowerLicenseUrl.EndsWith(".txt") &&
                    !lowerLicenseUrl.EndsWith(".md"))
                {
                    package.License = "(custom)";
                }
                else
                {
                    package.License = await httpClient.GetStringAsync(package.LicenseUrl.Replace("/blob/", "/raw/"));
                }
            }
        }

        static void ExportToJson(List<NugetPackage> packages)
        {
            var json = JsonConvert.SerializeObject(packages, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText("project_packages.json", json);
        }

        static void ExportAcknowledgements(List<NugetPackage> packages)
        {
            var result = "";

            foreach (var package in packages)
            {
                if (result != "")
                {
                    result += "\n\n".PadRight(30, '-') + "\n\n";
                }

                result += $"{package.Name}";
                result += $"\n\nAuthors: {package.Authors}";
                result += $"\nProject URL: {package.ProjectUrl}";
                result += $"\nLicense URL: {package.LicenseUrl}\n\n";
                result += package.License;
            }

            while (result.Contains("\n\n\n"))
            {
                result = result.Replace("\n\n\n", "\n\n");
            }

            File.WriteAllText("acknowledgements.txt", result);
        }

        static string MITLicense(string authors)
        {
            return @$"Copyright (c) {authors}

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
""Software""), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/ or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT.IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";
        }
    }
}
