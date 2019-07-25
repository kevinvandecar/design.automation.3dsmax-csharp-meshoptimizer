/////////////////////////////////////////////////////////////////////
// Copyright (c) Autodesk, Inc. All rights reserved
// Written by Forge Partner Development
//
// Permission to use, copy, modify, and distribute this software in
// object code form for any purpose and without fee is hereby granted,
// provided that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  AUTODESK, INC.
// DOES NOT WARRANT THAT THE OPERATION OF THE PROGRAM WILL BE
// UNINTERRUPTED OR ERROR FREE.
/////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.IO.Compression;

using Newtonsoft.Json;

using Autodesk.Max;

namespace Autodesk.Forge.Sample.DesignAutomation.Max
{
    /// <summary>
    /// Used to hold the parameters to change
    /// </summary>
    public class InputParams
    {
        public List<float> VertexPercents { get; set; }
        public bool KeepNormals { get; set; }
        public bool CollapseStack { get; set; }
        public bool CreateSVFPreview { get; set; }

    }
    /// <summary>
    /// Iterate entire scene to get all nodes
    /// Adds ProOptimizer modifier to each node
    /// </summary>
    static public class ParameterChanger
    {
        static List<IINode> m_sceneNodes = new List<IINode> { };


        /// <summary>
        /// This will return a modifier from the stack
        /// </summary>
        /// <param name="nodeToSearch"> Input node to search. </param>
        /// <param name="cid"> Input the class id of the modifier to find. </param>
        /// <returns> The found modifier or null if not found. </returns>
        static public IModifier GetModifier(IINode nodeToSearch, IClass_ID cid)
        {
            IGlobal global = Autodesk.Max.GlobalInterface.Instance;

            IIDerivedObject dobj = nodeToSearch.ObjectRef as IIDerivedObject;

            while (dobj != null)
            {
                int nmods = dobj.NumModifiers;
                for (int i = 0; i < nmods; i++)
                {
                    IModifier mod = dobj.GetModifier(i);
                    // have to compare ClassID Parts A and B separately. The equals operator is not 
                    // implemented so it will return false even when they are equal.
                    if ((mod.ClassID.PartA == cid.PartA) && (mod.ClassID.PartB == cid.PartB))
                        return mod;
                }
                dobj = dobj.ObjRef as IIDerivedObject;
            }

            return null;
        }
        //
        /// <summary>
        /// Adds an object space modifier to provided node (by handle).
        /// </summary>
        /// <param name="nodeHandle"> Input the node handle to add the modifier to. </param>
        /// <param name="cid"> Input the class id of the modifier add. </param>
        /// <returns> Returns 1 if successful or -1 if not. </returns>
        static public int AddOsmModifier(IINode node, IClass_ID cid)
        {
            try
            {

                IGlobal global = Autodesk.Max.GlobalInterface.Instance;
                IInterface14 ip = global.COREInterface14;

                IObject obj = node.ObjectRef;
                IIDerivedObject dobj = global.CreateDerivedObject(obj);
                object objMod = ip.CreateInstance(SClass_ID.Osm, cid as IClass_ID);
                IModifier mod = (IModifier)objMod;

                dobj.AddModifier(mod, null, 0); // top of stack
                node.ObjectRef = dobj;
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
                return -1;
            }

            return 1;
        }

        public enum ProOptimizerPBValues
        {
            optimizer_main_ratio = 0,
            optimizer_main_vertexcount,
            optimizer_main_calculate,
            optimizer_options_optmode,
            optimizer_options_keep_materials,
            optimizer_options_keep_uv,
            optimizer_options_lock_uv,
            optimizer_options_tolerance_uv,
            optimizer_options_keep_vc,
            optimizer_options_lock_vc,
            optimizer_options_merge_faces,
            optimizer_options_merge_faces_angle,
            optimizer_options_merge_points,
            optimizer_options_merge_points_threshold,
            optimizer_options_preserve_selection,
            optimizer_options_invert_selection,
            optimizer_options_tolerance_vc,
            optimizer_symmetry_mode,
            optimizer_symmetry_tolerance,
            optimizer_advanced_compact,
            optimizer_advanced_flip,
            optimizer_options_keep_normals,
            optimizer_options_normal_mode,
            optimizer_options_normal_threshold,
            optimizer_advanced_lock_points
        }

        /// <summary>
        /// Adds the Shell modifier to the provided node (by handle).
        /// </summary>
        /// <param name="nodeHandle"> Input the node handle to add the modifier to. </param>
        /// <param name="shellAmount"> Input the amount of shell thickness as float. </param>
        /// <returns> Returns 1 if successful or -1 if not. </returns>
        static public int AddOsmProoptimizer(IINode node, float VertexPercent, bool KeepNormals)
        {
            try
            {
                IGlobal global = Autodesk.Max.GlobalInterface.Instance;
                IInterface14 ip = global.COREInterface14;
                int t = ip.Time;

                // classID:#(1056067556, 1496462090) 
                IClass_ID cidOsmProoptimizer = global.Class_ID.Create(0x3EF24FE4, 0x5932330A);
                AddOsmModifier(node, cidOsmProoptimizer);

                IModifier mod = GetModifier(node, cidOsmProoptimizer);
                if (mod != null)
                {
                    // In order to get the "Calculate" parameter to trigger the modifier to execute, we have to enable some UI elements.
                    ip.CmdPanelOpen = true; // ensures the command panel in general is open
                    ip.SelectNode(node, true); // Select the node to make it active
                    ip.CommandPanelTaskMode = 2; // TASK_MODE_MODIFY. This makes the modifier panel active.
                    // Now we can set the parameters on the modifier, and at end "calculate" the results.
                    IIParamBlock2 pb = mod.GetParamBlock(0);
                    pb.SetValue((int)ProOptimizerPBValues.optimizer_main_ratio, t, VertexPercent, 0);
                    pb.SetValue((int)ProOptimizerPBValues.optimizer_options_keep_uv, t, 1, 0);
                    pb.SetValue((int)ProOptimizerPBValues.optimizer_options_keep_normals, t, 0, 0);
                    // There is no true way to know if this was valid/invalid for the mesh, so we check the outer level routine on triobject for changes. **
                    pb.SetValue((int)ProOptimizerPBValues.optimizer_main_calculate, t, 1, 0); 
                    ip.ClearNodeSelection(false);

                }
            }
            catch (Exception ex)
            {
                Debug.Print(ex.Message);
                return -1;
            }

            return 1;
        }
        //
        /// <summary>
        /// Recursively go through the scene and get all nodes
        /// Use the Autodesk.Max APIs to get the children nodes
        /// </summary>
        static private void GetSceneNodes(IINode node)
        {
            m_sceneNodes.Add(node);

            for (int i = 0; i < node.NumberOfChildren; i++)
                GetSceneNodes(node.GetChildNode(i));
        }

 
        static public string UpdateNodes(float vertexPercent, bool keepNormals, bool collapseStack, bool createSVFPreview = false)
        {
            IGlobal globalInterface = Autodesk.Max.GlobalInterface.Instance;
            IInterface14 coreInterface = globalInterface.COREInterface14;

            // start the scene process
            globalInterface.TheHold.Begin();

            IINode nodeRoot = coreInterface.RootNode;
            m_sceneNodes.Clear();
            GetSceneNodes(nodeRoot);

            List<IINode> optimizedNodes = new List<IINode> { };

            // Iterate each node in the scene file and process all meshes into ProOptimized meshes.
            foreach (IINode node in m_sceneNodes)
            {
                // Check for object assigned to node (could be something other than object)
                if (node.ObjectRef != null) { 
                    IObjectState os = node.ObjectRef.Eval(coreInterface.Time);
                    IObject objOriginal = os.Obj;
                    if (!objOriginal.IsSubClassOf(globalInterface.TriObjectClassID))
                    {
                        // If it is NOT, see if we can convert it...
                        if (objOriginal.CanConvertToType(globalInterface.TriObjectClassID) == 1)
                        {
                            objOriginal = objOriginal.ConvertToType(coreInterface.Time, globalInterface.TriObjectClassID);
                            ITriObject tri = objOriginal as ITriObject;
                            int val = tri.Mesh.NumVerts;
                            AddOsmProoptimizer(node, vertexPercent, keepNormals);
                            // get new mesh state
                            os = node.ObjectRef.Eval(coreInterface.Time);
                            tri = os.Obj as ITriObject;
                            // ** after modifier operation we can see if success by checking if the mesh size is different than before
                            if (val != tri.Mesh.NumVerts)
                            {
                                if (collapseStack)
                                    coreInterface.CollapseNode(node, true);
                                optimizedNodes.Add(node);
                            }
                        }
                    }
                }
            }


            int status;
            if (optimizedNodes.Count() > 0)
            {
                // Build result file name based on percentage used
                string full_filename = coreInterface.CurFilePath;
                string filename = coreInterface.CurFileName;
                vertexPercent = vertexPercent * 100;
                string stringVertexPercent = vertexPercent.ToString("F1");
                stringVertexPercent = stringVertexPercent.Replace('.', '_');
                string output = "outputFile-" + stringVertexPercent + ".max";
                string new_filename = full_filename.Replace(filename, output);
                status = coreInterface.SaveToFile(new_filename, true, false);

                // setup to export as FBX as well
                string outputFBX = new_filename.Replace(".max", ".fbx");
                string msCmdFbxExport = "exportFile \"" + outputFBX + "\" #noPrompt using:FBXEXP";
                bool fbxOk = globalInterface.ExecuteMAXScriptScript(msCmdFbxExport, false, null, false);

                if (createSVFPreview == true)
                {
                    string pathRoot = full_filename.Replace(filename, "");
                    string pathSVF = pathRoot + stringVertexPercent;
                    string outputSVF = pathSVF + "\\outputFile-" + stringVertexPercent + ".svf";
                    string outputZIP = pathRoot + "outputFile-" + stringVertexPercent + ".zip";

                    string msCmdSvfExport = "exportFile \"" + outputSVF + "\" #noPrompt";
                    bool svfOk = globalInterface.ExecuteMAXScriptScript(msCmdSvfExport, false, null, false);
                    ZipFile.CreateFromDirectory(pathSVF, outputZIP);
                }
                // put scene back for next iteration
                globalInterface.TheHold.Cancel();

                if ((status == 0) || (fbxOk == false)) // error saving max or fbx file
                    return null;

                return new_filename;
            }

            return null;
        }

    }

    /// <summary>
    /// This class is used to execute the automation. Above class could be connected to UI elements, or run by scripts directly.
    /// This class takes the input from JSON input and uses those values. This way it is more cohesive to web development.
    /// </summary>
    static public class RuntimeExecute
    {
        static public int ProOptimizeMesh()
        {
            IGlobal globalInterface = Autodesk.Max.GlobalInterface.Instance;
            IInterface14 coreInterface = globalInterface.COREInterface14;

            int count = 0;

            // Run entire code block with try/catch to help determine errors
            try
            {
                // read input parameters from JSON file
                InputParams inputParams = JsonConvert.DeserializeObject<InputParams>(File.ReadAllText("params.json"));
                /*InputParams inputParams = new InputParams
                {
                    VertexPercents = new List<float> { 0.225F, 0.426F, 0.752F, 0.895F },
                    KeepNormals = false,
                    CollapseStack = false,
                    CreateSVFPreview = true
                };*/

                //KDV need to add this to input
                //inputParams.CreateSVFPreview = true;

                List<string> solution_files = new List <string> { };
                string outputZIP = null;
                if (inputParams.CreateSVFPreview == true)
                {
                    string full_filename = coreInterface.CurFilePath;
                    string filename = coreInterface.CurFileName;
                    string pathRoot = full_filename.Replace(filename, "");
                    string stringVertexPercent = "100_0"; //orginal without any reduction.
                    string pathSVF = pathRoot + stringVertexPercent; 
                    string outputSVF = pathSVF + "\\outputFile-" + stringVertexPercent + ".svf";
                    outputZIP = pathRoot + "outputFile-" + stringVertexPercent + ".zip";

                    string msCmdSvfExport = "exportFile \"" + outputSVF + "\" #noPrompt";
                    bool svfOk = globalInterface.ExecuteMAXScriptScript(msCmdSvfExport, false, null, false);
                    ZipFile.CreateFromDirectory(pathSVF, outputZIP);
                    solution_files.Add(outputZIP); // add the 100%
                }

                foreach (float n in inputParams.VertexPercents)
                {
                    string status = ParameterChanger.UpdateNodes(n, inputParams.KeepNormals, inputParams.CollapseStack, inputParams.CreateSVFPreview);
                    if (status != null)
                    {
                        count += 1; // number of solutions successfully created as new scene files.
                        // add MAX files
                        solution_files.Add(status);
                        // add FBX files
                        status = status.Replace(".max", ".fbx");
                        solution_files.Add(status);
                        if (inputParams.CreateSVFPreview == true)
                        {
                            // add preview SVF zip files
                            status = status.Replace(".fbx", ".zip");
                            solution_files.Add(status);
                        }
                    }
                }

                if (solution_files.Count > 0)
                {
                    string zipName = @".\output.zip";
                    using (ZipArchive newZipFile = ZipFile.Open(zipName, ZipArchiveMode.Create))
                    {
                        foreach (string file in solution_files)
                        {
                            newZipFile.CreateEntryFromFile(file, System.IO.Path.GetFileName(file));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogTrace("Exception Error: " + e.Message);
                return -1; //fail
            }
            LogTrace("Changed {0} scenes.", count);
            return count; // 0+ means success, and how many objects were changed.
        }
        /// <summary>
        /// Information sent to this LogTrace will appear on the Design Automation output
        /// </summary>
        private static void LogTrace(string format, params object[] args)
        {
            IGlobal globalInterface = Autodesk.Max.GlobalInterface.Instance;
            IInterface14 coreInterface = globalInterface.COREInterface14;
            ILogSys log = coreInterface.Log;
            // Note flags are necessary to produce Design Automation output. This is same as C++:
            // SYSLOG_INFO | SYSLOG_IGNORE_VERBOSITY | SYSLOG_BROADCAST
            log.LogEntry(0x00000004 | 0x00040000 | 0x00010000, false, "", string.Format(format, args));
        }
    }
}