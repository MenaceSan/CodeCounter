using System;
using System.IO;
using System.Xml;

namespace CodeCounter
{
    public class ProjectReader : CodeReader
    {
        // Read a XML project file.  *.vcproj, *.vcxproj, *.csproj

        const string kInclude = "Include";
        const string kAddDeps = "AdditionalDependencies";
        const string kImportGroup = "ImportGroup";

        NameSpaces _namespaces;
        ProjectReference _proj;

        public ProjectReader(NameSpaces namespaces, ProjectReference proj)
        {
            _namespaces = namespaces;
            _proj = proj;
        }

        void AddPackageRef(string name)
        {
            if (name == null)
                return;

            string nameL = name.ToLower();
            if (_proj.PackageRefs.ContainsKey(nameL))
                return;
            var package = _namespaces.AddPackageRef(name);
            if (package == null)    // IsIgnored?
                return;
            _proj.PackageRefs.Add(nameL, package);
        }

        void AddImportRef(string filePath, bool optional)
        {
            // assume it exists ? .dll
            if (filePath == null)
                return;
            if (filePath.EndsWith(".targets", StringComparison.InvariantCultureIgnoreCase))
                filePath = filePath.Substring(0, filePath.Length - 8);
            if (filePath.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase))
                filePath = filePath.Substring(0, filePath.Length - 4);

            string nameL = Path.GetFileName(filePath).ToLower();
            AddPackageRef(nameL);
        }

        void AddProjectRef(string fileName, string dir)
        {
            // e.g. fileName = "..\\xxxx\\xxxx.vcxproj"
            if (fileName == null)
                return;
            string nameL = Path.GetFileName(fileName).ToLower();
            if (_proj.ProjectRefs.ContainsKey(nameL))
                return;
            var proj2 = _namespaces.AddProjectRef(Path.Combine(dir, fileName), false);
            if (proj2 == null)
                return;
            _proj.ProjectRefs.Add(nameL, proj2);
        }

        void AddDependency(string fileName, string dir)
        {
            // add a NAME.lib file. 

            if (string.IsNullOrWhiteSpace(fileName) || fileName.StartsWith("$(") || fileName.StartsWith("%(") || fileName == ".")
                return;

            string nameNE = fileName;
            if (nameNE.EndsWith(".lib", StringComparison.InvariantCultureIgnoreCase))
                nameNE = nameNE.Substring(0, fileName.Length - 4);

            string nameL = nameNE.ToLower();

            if (PackageLib.IsIgnored(nameL))
                return;

            // this might be a project or it might be a package. we dont really know.
            if (_proj.ProjectRefs.ContainsKey(nameL))
                return;
            if (_proj.PackageRefs.ContainsKey(nameL))
                return;

            // Is it a project ?
            var proj2 = _namespaces.AddProjectRef(Path.Combine(dir, nameNE), true);
            if (proj2 != null)
            {
                _proj.ProjectRefs.Add(nameL, proj2);
                return;
            }

            // Is it a package ? May have to search in path.
            var package = _namespaces.AddPackageRef(nameNE);
            if (package != null)
            {
                _proj.PackageRefs.Add(nameL, package);
                return;
            }

            AddError("Cant add dep");
        }

        void AddDependencies(string lineRaw, string dir)
        {
            string[] libs = lineRaw.Split(";");
            foreach (string lib in libs)
            {
                AddDependency(lib, dir);
            }
        }

        int ReadFile(StreamReader stream, string dir)
        {
            var settings = new XmlReaderSettings();
            settings.Async = true;

            using (XmlReader reader = XmlReader.Create(stream, settings))
            {
                bool isImportGroupNative = false;
                bool isOpenRef = false;
                string textBlock = null;
                while (reader.Read())
                {
                    switch (reader.NodeType)
                    {
                        case XmlNodeType.Element:
                            // Console.WriteLine("Start Element {0}", reader.Name);
                            switch (reader.Name)
                            {
                                case "SubSystem":
                                    // Console
                                    break;
                                case "OutputType":
                                    if (reader.Value == "Exe")
                                        _proj.IsExe = true;
                                    break;
                                case "ProjectReference":
                                    // <Project ><ItemGroup><ProjectReference Include="..\asdasdasd.vcxproj" />
                                    AddProjectRef(reader.GetAttribute(kInclude), dir);
                                    break;
                                case "PackageReference":
                                    // a lib.
                                    // <PackageReference Include="Plugin.Fingerprint" Version="2.1.1" />
                                    AddPackageRef(reader.GetAttribute(kInclude));
                                    break;
                                case kAddDeps:
                                    textBlock = "";
                                    break;

                                case kImportGroup:
                                    // Label="ExtensionTargets"
                                    string label = reader.GetAttribute("Label");
                                    isImportGroupNative = label == "ExtensionTargets" || label == "Shared";
                                    break;

                                case "Import":
                                    // can be used for import of props (ignore this)
                                    // can be used for nuget native includes. 

                                    if (!isImportGroupNative)
                                        break;
                                    // <ImportGroup Label="Shared"> <Import Project="..\..\Build\packages\XXXX.3.1.15\build\native\XXXX.targets" Condition="Exists('..\..\Build\packages\XXXX.3.1.15\build\native\XXXX.targets')" />
                                    string projName = reader.GetAttribute("Project");
                                    if (string.IsNullOrWhiteSpace(projName))
                                        break;
                                    string cond = reader.GetAttribute("Condition");
                                    AddImportRef(projName, !string.IsNullOrWhiteSpace(cond));
                                    break;

                                case "Reference":
                                    // Old fashioned way to include pacakges.
                                    // <ItemGroup> <Reference Include="TSS.Net, Version=2.1.1.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35, processorArchitecture=MSIL">
                                    // <HintPath> ..\..\Build\packages\Microsoft.TSS.2.1.1\lib\net46\TSS.Net.dll </ HintPath >
                                    isOpenRef = true;
                                    break;

                                case "HintPath":
                                    if (isOpenRef)
                                    {
                                        textBlock = "";
                                    }
                                    break;
                            }
                            break;
                        case XmlNodeType.Text:
                            // Console.WriteLine("Text Node: {0}", reader.Value);
                            if (textBlock != null)
                            {
                                textBlock += reader.Value;
                            }
                            break;
                        case XmlNodeType.EndElement:
                            // Console.WriteLine("End Element {0}", reader.Name);
                            switch (reader.Name)
                            {
                                case kAddDeps:
                                    // AdditionalDependencies have .lib files.
                                    // <Project><ItemDefinitionGroup><Link> ... <AdditionalDependencies>NAMEOFLIB</AdditionalDependencies>
                                    //  <AdditionalDependencies>NAME.lib;NAME.lib;NAME.lib;NAME.lib;NAME.lib;NAME.lib;NAME.lib;kernel32.lib;user32.lib;gdi32.lib;winspool.lib;comdlg32.lib;advapi32.lib;shell32.lib;ole32.lib;oleaut32.lib;uuid.lib;odbc32.lib;odbccp32.lib;%(AdditionalDependencies)</AdditionalDependencies>
                                    AddDependencies(textBlock, dir);
                                    textBlock = null;
                                    break;
                                case kImportGroup:
                                    isImportGroupNative = false;
                                    break;
                                case "Reference":
                                    isOpenRef = false;
                                    break;
                                case "HintPath":
                                    if (isOpenRef && textBlock != null)
                                    {
                                        AddImportRef(textBlock, false);
                                        textBlock = null;
                                    }
                                    break;
                            }
                            break;
                        case XmlNodeType.Whitespace:
                            break;
                        default:
                            // Console.WriteLine("Other node '{0}' with value '{1}'", reader.NodeType, reader.Value);
                            break;
                    }
                }
            }

            return 2;
        }


        public int ReadFile(string filePath)
        {
            // Load the Project file.
            // May have to search for it if its a lib.
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!File.Exists(filePath))
                    return -1;

                using (var rdr = new StreamReader(filePath))
                {
                    return ReadFile(rdr, dir);
                }
            }
            catch (Exception ex)
            {
                // TODO: Log error reading file.
                AddError("Project File Exception");
                if (ex.HResult < 0)
                    return ex.HResult;
                return -1; // E_FAIL
            }
        }
    }
}
