using System;
using System.Collections.Generic;
using System.Xml;
using System.IO;
using EnvDTE;
using Microsoft.VisualStudio.Shell.Interop;
using IRunningObjectTable = System.Runtime.InteropServices.ComTypes.IRunningObjectTable;
using IBindCtx = System.Runtime.InteropServices.ComTypes.IBindCtx;
using Microsoft.VisualStudio.Shell;

namespace UnityBuildMenuProject {
    enum buildConfiguration {
        debug32 = 0x0001,
        debug64 = 0x0010,
        release32 = 0x0100,
        release64 = 0x1000,
    }

    class ProjectParser {
        string nsName = "";
        static XmlDocument xdoc = new XmlDocument();
        XmlNamespaceManager nsmgr = new XmlNamespaceManager(xdoc.NameTable);

        int enableUnityBuild = 0;
        private int unitybuildMenuSetting;

        public void SetUnitybuildMenuinfo(int unitybuildMenuValue) {
            this.unitybuildMenuSetting = unitybuildMenuValue;
        }

        public int GetUnityBuildMenuInfo() {
            return unitybuildMenuSetting;
        }

        public void ModifyUnityBuildXML(string projFileName, string slnFileName, bool unitybuild, string uniqueName, bool settingMenuProject) {
            Guid guid = new Guid();
            
            string solutionName = slnFileName;
            object solutionObject = null;

            IRunningObjectTable rot;
            System.Runtime.InteropServices.ComTypes.IEnumMoniker enumMoniker;
            int retVal = GetRunningObjectTable(0, out rot);

            if (retVal == 0) {
                rot.EnumRunning(out enumMoniker);
                enumMoniker.Reset();
                IntPtr fetched = IntPtr.Zero;
                System.Runtime.InteropServices.ComTypes.IMoniker[] moniker = new System.Runtime.InteropServices.ComTypes.IMoniker[1];
                while (enumMoniker.Next(1, moniker, fetched) == 0) {
                    IBindCtx bindCtx;
                    CreateBindCtx(0, out bindCtx);
                    string displayName;
                    moniker[0].GetDisplayName(bindCtx, null, out displayName);

                    bool isPrebuildSolution = displayName.Contains(solutionName);
                    if (isPrebuildSolution) {
                        rot.GetObject(moniker[0], out solutionObject);
                        break;
                    }
                }
            }

            if (solutionObject != null) {

                Solution solution = solutionObject as Solution;
                EnvDTE.DTE dte = solution.DTE;
                Array projectsObject = (Array)dte.ActiveSolutionProjects;
                Project project;

                ServiceProvider serviceProvider = new ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)dte);
                IVsSolution vsSolution = (IVsSolution)serviceProvider.GetService(typeof(IVsSolution));
                if(vsSolution == null) {
                    return;
                }

                IVsHierarchy vsHierarchy = null;

                if (uniqueName == null) {
                    project = projectsObject.GetValue(0) as Project;
                    vsSolution.GetProjectOfUniqueName(project.UniqueName, out vsHierarchy);
                } else {
                    vsSolution.GetProjectOfUniqueName(uniqueName, out vsHierarchy);
                }
                vsSolution.GetGuidOfProject(vsHierarchy, out guid);

                IVsSolution4 vsSolution4 = (IVsSolution4)serviceProvider.GetService(typeof(SVsSolution));
                vsSolution4.UnloadProject(guid, (uint)_VSProjectUnloadStatus.UNLOADSTATUS_UnloadedByUser);

                ///////////////////////////////// xml수정 ///////////////////////////////
                if (settingMenuProject) {
                    int unitybuildMenuValue = SetUnitybuildEnableInfo(projFileName);
                    SetUnitybuildMenuinfo(unitybuildMenuValue);
                }

                if (unitybuild == true && settingMenuProject == false) {
                    SettingEnableUnityBuild(projFileName);
                } else if (unitybuild == false && settingMenuProject == false) {
                    SettingDisableUnityBuild(projFileName);
                }
                /////////////////////////////// xml수정 ///////////////////////////////

                vsSolution4.ReloadProject(guid);
            }
        }

        [System.Runtime.InteropServices.DllImport("ole32.dll")]
        private static extern void CreateBindCtx(int reserved, out IBindCtx ppbc);

        [System.Runtime.InteropServices.DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable prot);

        private void NameSpaceManager() {
            XmlNodeList rootList = xdoc.ChildNodes;
            foreach (XmlNode childOfRootList in rootList) {

                if (childOfRootList.Name.Equals("Project")) {
                    nsName = childOfRootList.Attributes["xmlns"].Value;
                    nsmgr.AddNamespace("ns", nsName);
                }
            }
        }

        private void SettingEnableUnityBuild(string projectFilePath) {
            xdoc.Load(projectFilePath);
            NameSpaceManager();
            
            List<string> cppFileList;
            enableUnityBuild = 0;
            
            AddMoaMoaUnityBuildinXML();
            bool precompileHeader = SettingPrecompiledHeader();
            cppFileList = FindCPPFiles(precompileHeader);
            AddUnityBuildCPPFile(cppFileList, projectFilePath, precompileHeader);
            ExcludeCPPInfo(enableUnityBuild);
            
            xdoc.Save(projectFilePath);
        }

        private void SettingDisableUnityBuild(string projectFilePath) {
            xdoc.Load(projectFilePath);
            NameSpaceManager();
            enableUnityBuild = 1;

            ExcludeCPPInfo(enableUnityBuild);

            xdoc.Save(projectFilePath);
        }


        private bool SettingPrecompiledHeader() {
            XmlNodeList xnode = xdoc.SelectNodes("ns:Project/ns:ItemDefinitionGroup/ns:ClCompile/ns:PrecompiledHeader", nsmgr);
            bool precompileHeaderStatus = false;

            if (xnode.Count == 0) {
                precompileHeaderStatus = false;
            }
            foreach(XmlNode precompileNode in xnode) {
                if(precompileNode.InnerText == "Use") {
                    precompileHeaderStatus = true;
                }
            }
            return precompileHeaderStatus;
        }

        private void AddMoaMoaUnityBuildinXML() {
            List<string> includecppName = new List<string>();
            XmlNodeList xnode = xdoc.SelectNodes("ns:Project/ns:ItemGroup", nsmgr);

            foreach (XmlNode itemNode in xnode) {
                XmlNodeList itemchildnode = itemNode.ChildNodes;

                int clNodeCnt = itemchildnode.Count;
                foreach (XmlNode clCompileNode in itemchildnode) {

                    if (clCompileNode.Name.Equals("ClCompile")) {
                        for (int i = 0; i < clNodeCnt; ++i) {
                            XmlNode includeNames = itemchildnode.Item(i);
                            string cppNameValues = includeNames.Attributes["Include"].Value;
                            includecppName.Add(cppNameValues);
                        }

                        if (includecppName.Contains("MoaMoa_unitybuild.cpp")) {
                            return;
                        }

                        XmlElement xelm = xdoc.CreateElement("ClCompile", xdoc.DocumentElement.NamespaceURI);
                        xelm.SetAttribute("Include", "MoaMoa_unitybuild.cpp");
                        itemNode.AppendChild(xelm);

                        break;
                    }
                }
            }
        }

        private List<string> FindCPPFiles(bool precompileHeader) {
            XmlNodeList xnode = xdoc.SelectNodes("ns:Project/ns:ItemGroup", nsmgr);
            List<string> cppList = new List<string>();
            foreach (XmlNode itemNode in xnode) {
                XmlNodeList clCompileNode = itemNode.ChildNodes;
                foreach (XmlNode cppNode in clCompileNode) {
                    if (cppNode.Name.Equals("ClCompile")) {
                        foreach (XmlAttribute attribute in cppNode.Attributes) {
                            string cppName = attribute.Value;
                            if (cppName == "MoaMoa_unitybuild.cpp") {
                                continue;
                            }
                            if ( precompileHeader == true && cppName.ToLower() == "stdafx.cpp") {
                                    continue;
                            }
                            cppList.Add(cppName);
                        }
                    }
                }
            }
            return cppList;
        }

        private void AddUnityBuildCPPFile(List<string> cppFileNames, string projectFilePath, bool precompileHeader) {
            string unityName = "MoaMoa_unitybuild.cpp";
            projectFilePath = Path.GetDirectoryName(projectFilePath);
            string cppfilePath = Path.Combine(projectFilePath, unityName);

            FileStream fs = File.Create(cppfilePath);
            StreamWriter sw = new StreamWriter(fs);

            int cppCount = cppFileNames.Count;
            for (int i = 0; i < cppCount; ++i) {

                if (File.Exists(cppfilePath)) {
                    const string quote = "\"";
                    if (precompileHeader == true) {
                        string stdafxHeader = "#include" + quote + "stdafx.h" + quote;
                        sw.WriteLine(stdafxHeader);
                        precompileHeader = false;
                    }
                    string innerName = "#include" + quote + cppFileNames[i] + quote;
                    sw.WriteLine(innerName);
                } else {
                    File.Create(cppfilePath);
                }
            }
            sw.Close();
            fs.Close();

            cppFileNames.Clear();
        }

        private void ExcludeCPPInfo(int enableUnityBuild) {
            XmlNodeList xnode = xdoc.SelectNodes("ns:Project/ns:ItemGroup/ns:ClCompile", nsmgr);

            int status = 0;

            foreach (XmlNode clCompileNode in xnode) {
                string include = clCompileNode.Attributes["Include"].Value;
                
                if (clCompileNode.ChildNodes == null && enableUnityBuild == 1) {
                    CreateExcludedFromBuild(include, clCompileNode, status);
                }

                XmlNodeList clcompileChild = clCompileNode.ChildNodes;
                foreach (XmlNode excludedNode in clcompileChild) {
                    if (excludedNode.Name.Equals("ExcludedFromBuild")) {
                        if (include.ToLower() == "stdafx.cpp") {
                            excludedNode.InnerText = "false";
                            continue;
                        }
                        XmlAttributeCollection attribute = excludedNode.Attributes;
                        string conditionValue = attribute.GetNamedItem("Condition").Value;
                        if (conditionValue == "'$(Configuration)|$(Platform)'=='Debug|Win32'") {
                            if (enableUnityBuild == 1) {
                                excludedNode.InnerText = "false";
                            } else {
                                excludedNode.InnerText = "true";
                            }
                            status |= (int)buildConfiguration.debug32;
                        }
                        if (conditionValue == "'$(Configuration)|$(Platform)'=='Debug|x64'") {
                            if (enableUnityBuild == 1) {
                                excludedNode.InnerText = "flase";
                            } else {
                                excludedNode.InnerText = "true";
                            }
                            status |= (int)buildConfiguration.debug64;
                        }
                        if (conditionValue == "'$(Configuration)|$(Platform)'=='Release|Win32'") {
                            if (enableUnityBuild == 1) {
                                excludedNode.InnerText = "false";
                            } else {
                                excludedNode.InnerText = "true";
                            }
                            status |= (int)buildConfiguration.release32;
                        }
                        if (conditionValue == "'$(Configuration)|$(Platform)'=='Release|x64'") {
                            if (enableUnityBuild == 1) {
                                excludedNode.InnerText = "false";
                            } else {
                                excludedNode.InnerText = "true";
                            }
                            status |= (int)buildConfiguration.release64;
                        }

                        if (include == "MoaMoa_unitybuild.cpp") {
                            if (conditionValue == "'$(Configuration)|$(Platform)'=='Debug|Win32'") {
                                if (enableUnityBuild == 1) {
                                    excludedNode.InnerText = "true";
                                } else {
                                    excludedNode.InnerText = "false";
                                }
                                status |= (int)buildConfiguration.debug32;
                            }
                            if (conditionValue == "'$(Configuration)|$(Platform)'=='Debug|x64'") {
                                if (enableUnityBuild == 1) {
                                    excludedNode.InnerText = "true";
                                } else {
                                    excludedNode.InnerText = "false";
                                }
                                status |= (int)buildConfiguration.debug64;
                            }
                            if (conditionValue == "'$(Configuration)|$(Platform)'=='Release|Win32'") {
                                if (enableUnityBuild == 1) {
                                    excludedNode.InnerText = "true";
                                } else {
                                    excludedNode.InnerText = "false";
                                }
                                status |= (int)buildConfiguration.release32;
                            }
                            if (conditionValue == "'$(Configuration)|$(Platform)'=='Release|x64'") {
                                if (enableUnityBuild == 1) {
                                    excludedNode.InnerText = "true";
                                } else {
                                    excludedNode.InnerText = "false";
                                }
                                status |= (int)buildConfiguration.release64;
                            }
                        }
                    }
                }
                CreateExcludedFromBuild(include, clCompileNode, status);
                status = 0;
            }
        }

        private void CreateExcludedFromBuild(string includeValue, XmlNode clCompileNode, int status) {
            XmlElement excludeDebug32 = xdoc.CreateElement("ExcludedFromBuild", xdoc.DocumentElement.NamespaceURI);
            excludeDebug32.SetAttribute("Condition", "'$(Configuration)|$(Platform)'=='Debug|Win32'");
            XmlElement excludeDebug64 = xdoc.CreateElement("ExcludedFromBuild", xdoc.DocumentElement.NamespaceURI);
            excludeDebug64.SetAttribute("Condition", "'$(Configuration)|$(Platform)'=='Debug|x64'");
            XmlElement excludeRelease32 = xdoc.CreateElement("ExcludedFromBuild", xdoc.DocumentElement.NamespaceURI);
            excludeRelease32.SetAttribute("Condition", "'$(Configuration)|$(Platform)'=='Release|Win32'");
            XmlElement excludeRelease64 = xdoc.CreateElement("ExcludedFromBuild", xdoc.DocumentElement.NamespaceURI);
            excludeRelease64.SetAttribute("Condition", "'$(Configuration)|$(Platform)'=='Release|x64'");

            if (includeValue == "MoaMoa_unitybuild.cpp" || includeValue.ToLower() == "stdafx.cpp") {
                excludeDebug32.InnerText = "false";
                excludeDebug64.InnerText = "false";
                excludeRelease32.InnerText = "false";
                excludeRelease64.InnerText = "false";
            } else {
                excludeDebug32.InnerText = "true";
                excludeDebug64.InnerText = "true";
                excludeRelease32.InnerText = "true";
                excludeRelease64.InnerText = "true";
            }

            if ((status & (int)buildConfiguration.debug32) == 0) {
                clCompileNode.AppendChild(excludeDebug32);
            }
            if ((status & (int)buildConfiguration.debug64) == 0) {
                clCompileNode.AppendChild(excludeDebug64);
            }
            if ((status & (int)buildConfiguration.release32) == 0) {
                clCompileNode.AppendChild(excludeRelease32);
            }
            if ((status & (int)buildConfiguration.release64) == 0) {
                clCompileNode.AppendChild(excludeRelease64);
            }
        }

        public int SetUnitybuildEnableInfo(string projectFilePath) {
            xdoc.Load(projectFilePath);
            NameSpaceManager();
            int menuStatus = 0;
            bool moamoaUnitybuild = false;

            XmlNodeList xnode = xdoc.SelectNodes("ns:Project/ns:ItemGroup/ns:ClCompile", nsmgr);
            foreach (XmlNode clCompileNode in xnode) {
                string include = clCompileNode.Attributes["Include"].Value;
                if (clCompileNode.ChildNodes.Count == 0) {
                    return 2;
                }

                XmlNodeList clcompileChild = clCompileNode.ChildNodes;
                foreach (XmlNode excludedNode in clcompileChild) {
                    if (excludedNode.Name.Equals("ExcludedFromBuild")) {
                        XmlAttributeCollection attribute = excludedNode.Attributes;
                        string conditionValue = attribute.GetNamedItem("Condition").Value;

                        if (include == "MoaMoa_unitybuild.cpp") {
                            moamoaUnitybuild = true;
                            if (conditionValue == "'$(Configuration)|$(Platform)'=='Debug|Win32'") {
                                if (excludedNode.InnerText == "true") {
                                    menuStatus = 1;
                                } else {
                                    menuStatus = 0;
                                }
                            }
                            if (conditionValue == "'$(Configuration)|$(Platform)'=='Debug|x64'") {
                                if (excludedNode.InnerText == "true") {
                                    menuStatus = 1;
                                } else {
                                    menuStatus = 0;
                                }
                            }
                            if (conditionValue == "'$(Configuration)|$(Platform)'=='Release|Win32'") {
                                if (excludedNode.InnerText == "true") {
                                    menuStatus = 1;
                                } else {
                                    menuStatus = 0;
                                }
                            }
                            if (conditionValue == "'$(Configuration)|$(Platform)'=='Release|x64'") {
                                if (excludedNode.InnerText == "true") {
                                    menuStatus = 1;
                                } else {
                                    menuStatus = 0;
                                }
                            }
                        } 
                    }
                }
            }
            if (moamoaUnitybuild == false) {
                return 2;
            }
            xdoc.Save(projectFilePath);
            return menuStatus;
        }
    }
}
