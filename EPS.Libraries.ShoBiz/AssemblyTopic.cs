using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Linq;
using EndpointSystems.OrchestrationLibrary;
using Microsoft.BizTalk.ExplorerOM;

namespace EndpointSystems.BizTalk.Documentation
{
    /// <summary>
    /// Adds the BizTalk assembly to the documentation.
    /// </summary>
    /// <remarks>Token file value: appName + ".Resources." + AssemblyQualifiedName (DisplayName)</remarks>
    public class AssemblyTopic : TopicFile, IDisposable
    {
        private readonly string assyName;
        private readonly BackgroundWorker assyWorker;
        private string displayName;
        public AssemblyTopic(string btsAppName, string btsAssemblyName, string basePath)
        {
            appName = btsAppName;
            path = basePath;
            assyName = btsAssemblyName;
            tokenId = CleanAndPrep(appName + ".Assemblies." + btsAssemblyName);
            TimerStart();
            //set the topic token
            TokenFile.GetTokenFile().AddTopicToken(tokenId, id);
            assyWorker = new BackgroundWorker();
            assyWorker.DoWork += assyWorker_DoWork;
            assyWorker.RunWorkerCompleted += assyWorker_RunWorkerCompleted;
            assyWorker.RunWorkerAsync();
        }

        public void Dispose()
        {
           if (null != assyWorker) assyWorker.Dispose();
        }

        private void assyWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            lock (this)
            {
                ReadyToSave = true;
            }
            TimerStop();
        }

        private void assyWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            BtsCatalogExplorer bce = new BtsCatalogExplorer();
            try
            {
                bce.ConnectionString = CatalogExplorerFactory.CatalogExplorer().ConnectionString;
            //get the object
            BtsAssembly assy = bce.Assemblies[assyName];
            displayName = assy.Name;
            XElement root = CreateDeveloperConceptualElement();

            //build the document structure

            #region assembly info

            root.Add(new XElement(xmlns + "section",
                                  new XElement(xmlns + "title", new XText("Assembly Information")),
                                  new XElement(xmlns + "content",
                                               new XElement(xmlns + "table",
                                                            new XElement(xmlns + "tableHeader",
                                                                         new XElement(xmlns + "row",
                                                                                      new XElement(xmlns + "entry",
                                                                                                   new XText("Property")),
                                                                                      new XElement(xmlns + "entry",
                                                                                                   new XText("Value")))),
                                                            new XElement(xmlns + "row",
                                                                         new XElement(xmlns + "entry", new XText("Name")),
                                                                         new XElement(xmlns + "entry", new XText(assy.Name))),
                                                            new XElement(xmlns + "row",
                                                                         new XElement(xmlns + "entry", new XText("Display Name")),
                                                                         new XElement(xmlns + "entry",
                                                                                      new XText(assy.DisplayName))),
                                                            new XElement(xmlns + "row",
                                                                         new XElement(xmlns + "entry", new XText("Version")),
                                                                         new XElement(xmlns + "entry", new XText(assy.Version))),
                                                            new XElement(xmlns + "row",
                                                                         new XElement(xmlns + "entry",
                                                                                      new XText("Public Key Token")),
                                                                         new XElement(xmlns + "entry", assy.PublicKeyToken))))
                         ));

            #endregion

            List<XElement> elems = new List<XElement>();

            #region list orchestrations

            if (assy.Orchestrations.Count > 0)
            {
                foreach (BtsOrchestration orchestration in assy.Orchestrations)
                {
                    elems.Add(new XElement(xmlns + "listItem", new XElement(xmlns + "token", CleanAndPrep(appName + ".Orchestrations." +  orchestration.FullName))));
                }
                XElement list = new XElement(xmlns + "list", new XAttribute("class", "bullet"),elems.ToArray());

                root.Add(new XElement(xmlns + "section",
                                      new XElement(xmlns + "title", new XText("Orchestrations")),
                                      new XElement(xmlns + "content",
                                                   new XElement(xmlns + "para",
                                                                new XText(
                                                                    "This assembly contains the following orchestrations:")),
                                                   list)));
            }

            #endregion

            #region list pipelines

            if (assy.Pipelines != null && assy.Pipelines.Count > 0)
            {
                elems.Clear();
                foreach (Pipeline pipeline in assy.Pipelines)
                {
                    elems.Add(new XElement(xmlns + "listItem",
                                          new XElement(xmlns + "token", CleanAndPrep(pipeline.AssemblyQualifiedName))));
                }
                XElement list = new XElement(xmlns + "list", new XAttribute("class", "bullet"),elems.ToArray());
                root.Add(AddListSection("Pipelines", "This assembly defines the following pipelines:", list));
            }

            #endregion

            #region port types

            //if (assy.PortTypes != null && assy.PortTypes.Count > 0)
            //{
            //    XElement list = new XElement(xmlns + "list", new XAttribute("class", "bullet"));
            //    foreach (PortType portType in assy.PortTypes)
            //    {
            //        list.Add(new XElement(xmlns + "listItem",
            //                              new XElement(xmlns + "token", CleanAndPrep(appName + ".PortTypes." + portType.FullName))));
            //    }
            //    root.Add(AddListSection("Port Types", "This assembly defines the following port types:", list));
            //}

            #endregion

            #region list schemas

            if (assy.Schemas != null && assy.Schemas.Count > 0)
            {
                elems.Clear();
                foreach (Schema schema  in assy.Schemas)
                {
                    elems.Add(new XElement(xmlns + "listItem", new XElement(xmlns + "token", CleanAndPrep(appName + ".Schemas." + schema.FullName))));
                }

                XElement list = new XElement(xmlns + "list", new XAttribute("class", "bullet"),elems.ToArray());
                root.Add(AddListSection("Schemas", "This assembly contains the following schemas:", list));
            }

            #endregion list schemas

            #region list transforms

            if (assy.Transforms != null && assy.Transforms.Count > 0)
            {
                foreach (Transform trans in assy.Transforms)
                {
                    elems.Add(new XElement(xmlns + "listItem", new XElement(xmlns + "token", CleanAndPrep(appName + ".Transforms." + trans.FullName))));
                }

                XElement list = new XElement(xmlns + "list", new XAttribute("class", "bullet"),elems.ToArray());
                root.Add(AddListSection("Transforms", "This assembly contains the following maps:", list));
            }

            #endregion

                if (doc.Root != null) doc.Root.Add(root);
            }
            catch(Exception ex)
            {
                HandleException("BtsAssemblyTopic.DoWork",ex);
            }
            finally
            {
                bce.Dispose();
            }
        }

        public XElement GetContentLayout()
        {
            return new XElement("Topic",
                                new XAttribute("id", id),
                                new XAttribute("visible", "true"),
                                new XAttribute("title", displayName));
        }

    }
}