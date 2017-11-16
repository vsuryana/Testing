using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.Taxonomy;
using OfficeDevPnP.Core;
using System;
using System.Linq;
using System.Xml;

namespace IMD.SP.SiteColumnAndContentTypes
{
    class Program
    {
        //New code is added to Venkata branch for testing
        // public static string filename = @"D:\Venkata\DMS\SiteColumnsContentTypes\SiteColumnsContentTypes\InputData\IMDData.xml";         
        public static XmlDocument xmlDoc = new XmlDocument();
        static void Main(string[] args)
        {
            //Loading Input Data from XML File
            Console.WriteLine("Please provide XML File Path");
            var filePath = Console.ReadLine();
            if (System.IO.File.Exists(filePath))
            {
                xmlDoc.Load(filePath);
                XmlNode Connection = xmlDoc.SelectSingleNode("/Inputs/ConnectSPOnline");
                XmlNode SiteColumns = xmlDoc.SelectSingleNode("/Inputs/SiteColumns");
                XmlNode ContentTypes = xmlDoc.SelectSingleNode("/Inputs/ContentTypes");
                AuthenticationManager authManager = new AuthenticationManager();
                try
                {
                    // Get and set the client context & Connects to SharePoint online site using inputs provided   
                    using (var clientContext = authManager.GetSharePointOnlineAuthenticatedContextTenant(Connection.Attributes["SiteURL"].Value.ToString(), Connection.Attributes["UserName"].Value.ToString(), Connection.Attributes["Password"].Value.ToString()))
                    {
                        Console.WriteLine("Creating Site Columns.........");
                        CreatingSiteColumns(SiteColumns, clientContext);
                        Console.WriteLine("Creating Site Content Types........");
                        CreatingContentTypes(ContentTypes, clientContext);
                        Console.ReadKey();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error Message: " + ex.Message);
                    Console.ReadKey();
                }
            }
            else
            {
                Console.WriteLine("File doesn't exist in given path");
                Console.ReadKey();
            }
        }
        static void CreatingSiteColumns(XmlNode sitecolumns, ClientContext clientcontext)
        {
            //Creating Site Columns
            foreach (XmlNode node in sitecolumns.ChildNodes)
            {
                //Checking Site Column Exist or not
                if (clientcontext.Web.FieldExistsByName(node.Attributes["InternalName"].Value))
                {
                    Console.WriteLine(node.Attributes["InternalName"].Value + " exists in the site");
                }
                else
                {
                    //Creating Managed Meta Data Site Columns
                    if (node.Attributes["MMD"].Value != "0")
                    {
                        CreateManagedMetaDataSiteColumns(clientcontext, node.Attributes["DisplayName"].Value, node.Attributes["InternalName"].Value, node.Attributes["GroupName"].Value, node.Attributes["MMDValue"].Value);
                    }
                    //Creating Normal Site Columns
                    else
                    {
                        CreateNormalSiteColumns(clientcontext, node.Attributes["DisplayName"].Value, node.Attributes["InternalName"].Value, node.Attributes["GroupName"].Value, node.Attributes["Type"].Value);
                    }
                }
            }
        }
        static void CreatingContentTypes(XmlNode contenttypes, ClientContext clientcontext)
        {
            var contentTypes = clientcontext.Web.ContentTypes;
            foreach (XmlNode node in contenttypes.ChildNodes)
            {
                if (!CheckContentType(clientcontext, node.Attributes["Name"].Value))
                {
                    ContentTypeCreationInformation CT = new ContentTypeCreationInformation()
                    {
                        Group = node.Attributes["Group"].Value,
                        Name = node.Attributes["Name"].Value
                    };
                    var contentType = contentTypes.Add(CT);
                    clientcontext.Load(contentType);
                    clientcontext.ExecuteQuery();
                    Console.WriteLine("New Content Type" + node.Attributes["Name"].Value + " has been created");
                    Console.WriteLine("Adding Site Columns to " + node.Attributes["Name"].Value + "Content Type");
                    foreach (XmlNode childnode in node.ChildNodes)
                    {

                        AddSiteColumnsToContentType(clientcontext, childnode.Attributes["Name"].Value, node.Attributes["Name"].Value);
                        Console.WriteLine(childnode.Attributes["Name"].Value + " is added to " + node.Attributes["Name"].Value + " Content Type");
                    }
                }
                else
                {
                    Console.WriteLine("Content Type " + node.Attributes["Name"].Value + " is already exists now we are site columns......");
                    foreach (XmlNode childnode in node.ChildNodes)
                    {

                        AddSiteColumnsToContentType(clientcontext, childnode.Attributes["Name"].Value, node.Attributes["Name"].Value);
                        Console.WriteLine(childnode.Attributes["Name"].Value + " is added to " + node.Attributes["Name"].Value + " Content Type");
                    }

                }
            }
        }

        static void CreateNormalSiteColumns(ClientContext cContext, string displayname, string internalname, string group, string type)
        {
            FieldType fieldType = new FieldType();          
            if (type == "Text") { fieldType = FieldType.Text; }
            if (type == "Note") { fieldType = FieldType.Note; }
            if (type == "Integer") { fieldType = FieldType.Integer; }
            if (type == "DateTime") { fieldType = FieldType.DateTime; }
            if (type == "Number") { fieldType = FieldType.Number; }
            if (type == "Choice") { fieldType = FieldType.Choice; }         
            if (type == "Boolean") { fieldType = FieldType.Boolean; }
            if (type == "Currency") { fieldType = FieldType.Currency; }
            // Field Creation Parameters  
            OfficeDevPnP.Core.Entities.FieldCreationInformation newFieldInfo = new OfficeDevPnP.Core.Entities.FieldCreationInformation(fieldType)
            {
                DisplayName = displayname,
                InternalName = internalname,
                Group = group,
                Id = Guid.NewGuid()
            };
            // Creates new Field  
            Field newField = cContext.Site.RootWeb.CreateField(newFieldInfo);
            Console.WriteLine("New Site Column" + newField.Title + " has been created");
        }
        static void CreateManagedMetaDataSiteColumns(ClientContext cContext, string displayname, string internalname, string group, string mmdvalue)
        {
            Web rootWeb = cContext.Site.RootWeb;
            Field field = rootWeb.Fields.AddFieldAsXml("<Field DisplayName='" + displayname + "' Name='" + internalname + "' ID='{" + Guid.NewGuid() + "}' Group='" + group + "' Type='TaxonomyFieldTypeMulti' />", false, AddFieldOptions.AddFieldInternalNameHint);
            cContext.ExecuteQuery();
            Guid termStoreId = Guid.Empty;
            Guid termSetId = Guid.Empty;
            GetTaxonomyFieldInfo(cContext, mmdvalue, out termStoreId, out termSetId);
            // Retrieve as Taxonomy Field
            TaxonomyField taxonomyField = cContext.CastTo<TaxonomyField>(field);
            taxonomyField.SspId = termStoreId;
            taxonomyField.TermSetId = termSetId;
            taxonomyField.TargetTemplate = String.Empty;
            taxonomyField.AnchorId = Guid.Empty;
            taxonomyField.Update();
            cContext.ExecuteQuery();
            Console.WriteLine("New " + displayname + " Column is created");
        }

        static void AddSiteColumnsToContentType(ClientContext clientContext, string fieldname, string contenttypename)
        {
            Web rootWeb = clientContext.Site.RootWeb;
            if (rootWeb.FieldExistsByName(fieldname))
            {
                Field sitecolumn = rootWeb.Fields.GetByInternalNameOrTitle(fieldname);
                //ContentType sessionContentType = rootWeb.ContentTypes.GetById("0x0100BDD5E43587AF469CA722FD068065DF5D");
                ContentType siteContentType = rootWeb.GetContentTypeByName(contenttypename);
                siteContentType.FieldLinks.Add(new FieldLinkCreationInformation
                {
                    Field = sitecolumn
                });
                siteContentType.Update(true);
                clientContext.ExecuteQuery();
            }
            else
            {
                Console.WriteLine("Site Column" + fieldname + " doesn't exist");
            }
        }
        static void GetTaxonomyFieldInfo(ClientContext clientContext, string TermsetName, out Guid termStoreId, out Guid termSetId)
        {
            termStoreId = Guid.Empty;
            termSetId = Guid.Empty;
            TaxonomySession session = TaxonomySession.GetTaxonomySession(clientContext);
            TermStore termStore = session.GetDefaultSiteCollectionTermStore();
            TermSetCollection termSets = termStore.GetTermSetsByName(TermsetName, 1033);
            clientContext.Load(termSets, tsc => tsc.Include(ts => ts.Id));
            clientContext.Load(termStore, ts => ts.Id);
            clientContext.ExecuteQuery();
            termStoreId = termStore.Id;
            termSetId = termSets.FirstOrDefault().Id;
        }
        static Boolean CheckContentType(ClientContext clientContext, string ctName)
        {
            Boolean flag = false;
            var contentTypes = clientContext.Web.ContentTypes;
            clientContext.Load(contentTypes);
            clientContext.ExecuteQuery();
            foreach (ContentType c in contentTypes)
            {
                if (c.Name == ctName)
                {
                    flag = true; break;
                }
            }
            return flag;
        }
    }
}
