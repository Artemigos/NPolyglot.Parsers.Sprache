using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Reflection;
using System.IO;
using Sprache;

namespace NPolyglot.Parsers.Sprache
{
    public class SpracheParse : AppDomainIsolatedTask
    {
        [Required]
        public ITaskItem ParsersDll { get; set; }

        [Required]
        public ITaskItem[] DslCodeFiles { get; set; }

        [Output]
        public ITaskItem[] DslCodeWithMetadata { get; set; }

        public override bool Execute()
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

                // set up separate AppDomain for parsers DLL
                var parsersPath = Path.GetFullPath(ParsersDll.ItemSpec);
                var parsersAssembly = Assembly.LoadFile(parsersPath);

                // extract parser types
                var parserTypes = parsersAssembly.GetTypes().Where(CheckIfParser);
                var parsersDict = parserTypes.ToDictionary(
                    x => (string)GetExportNameProperty(x).GetValue(null),
                    x => (Func<string, object>)GetParseMethod(x).CreateDelegate(typeof(Func<string, object>)));

                // process code files
                DslCodeWithMetadata = DslCodeFiles.Select(x => new TaskItem(x)).ToArray();
                foreach (var file in DslCodeWithMetadata.Where(IsFileValid))
                {
                    // find matching parser
                    var parserName = file.GetMetadata("Parser");
                    Func<string, object> parser;
                    if (!parsersDict.TryGetValue(parserName, out parser))
                    {
                        Log.LogError("Could not find parser '{0}' for file '{1}'.", parserName, file.ItemSpec);
                        return false;
                    }

                    // parse input
                    var codeContent = file.GetMetadata("Content");
                    dynamic parseResult = parser(codeContent);

                    // serialize generated object
                    var objectContent = Newtonsoft.Json.JsonConvert.SerializeObject(parseResult);
                    file.SetMetadata("Object", objectContent);
                }

                return true;
            }
            catch (Exception e)
            {
                Log.LogError("Failed to parse DSL files: {0}", e);
                return false;
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
            }
        }

        private bool CheckIfParser(Type t) =>
            GetParseMethod(t) != null && GetExportNameProperty(t) != null;

        private MethodInfo GetParseMethod(Type t) =>
            t.GetMethods().FirstOrDefault(x => x.Name == "ParseString" && x.ReturnType == typeof(object) && x.IsStatic && x.IsPublic);

        private PropertyInfo GetExportNameProperty(Type t) =>
            t.GetProperties().FirstOrDefault(x => x.Name == "ExportName" && x.PropertyType == typeof(string) && x.GetGetMethod().IsStatic && x.GetGetMethod().IsPublic);

        private bool IsFileValid(ITaskItem item) =>
            item.MetadataNames.Cast<string>().Contains("Parser") &&
            item.MetadataNames.Cast<string>().Contains("Content");

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) =>
            AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.FullName == args.Name) ?? FindInMyDir(args.Name);

        private Assembly FindInMyDir(string fullName)
        {
            var startingName = fullName.Split(',')[0];
            var localDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var assemblies = Directory.GetFiles(localDir, $"{startingName}.dll");
            var found = assemblies.FirstOrDefault();

            if (found == null)
            {
                return null;
            }

            return Assembly.LoadFile(found);
        }
    }
}
