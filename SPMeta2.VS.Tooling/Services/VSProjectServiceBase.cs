﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using EnvDTE;
using SPMeta2.VS.Tooling.Consts;
using SPMeta2.VS.Tooling.Options;
using SPMeta2.VS.Tooling.Utils;

namespace SPMeta2.VS.Tooling.Services
{
    public abstract class VSProjectSetupServiceBase<TOptions>
       where TOptions : M2ProjectOptions
    {
        public TOptions ProjectOptions { get; set; }

        public abstract void SetupM2IntranetProject(Project vsProject, TOptions options);

        public virtual void MapProjectProperties(Dictionary<string, string> replacementsDictionary, object _projectOptions)
        {
            // replacementsDictionary.Add("M2ProjectPrefix", _options.ProjectPrefix);
            //replacementsDictionary.Add("$M2Prefix$", _options.);

            // sync M2Prj settings
            var props = _projectOptions.GetType().GetProperties();

            foreach (var prop in props)
            {
                var propValue = prop.GetValue(_projectOptions);
                var propValueString = string.Empty;

                if (propValue != null)
                    propValueString = propValue.ToString();

                var propName = string.Format("$M2{0}$", prop.Name);
                var normalPropName = string.Format("M2{0}", prop.Name);

                // M2-based props go as it is
                if (prop.Name.ToUpper().StartsWith("M2"))
                    normalPropName = prop.Name;

                if (!replacementsDictionary.Keys.Contains(propName))
                    replacementsDictionary.Add(propName, propValueString);
                else
                    replacementsDictionary[propName] = propValueString;

                if (!replacementsDictionary.Keys.Contains(normalPropName))
                    replacementsDictionary.Add(normalPropName, propValueString);
                else
                    replacementsDictionary[normalPropName] = propValueString;
            }

            // sync additional stuff
            replacementsDictionary.Add("$rootnamespace$", replacementsDictionary["$safeprojectname$"]);
            replacementsDictionary.Add("M2RootNamespace", replacementsDictionary["$safeprojectname$"]);
        }

        public abstract bool UpdateVSProjectNuGetPackages(TOptions options, string vsDefPath);

        protected virtual void HandleExcludedFiles(Project project, IEnumerable<string> fileNames)
        {
            var projectItems2Delete = new List<ProjectItem>();

            var it = new ProjectItemIterator(new[] { project }).GetEnumerator();

            while (it.MoveNext())
            {
                var item = it.Current;

                if (!string.IsNullOrEmpty(item.Name))
                {
                    if (fileNames.Any(n => n.ToUpper() == item.Name.ToUpper()))
                    {
                        projectItems2Delete.Add(item);

                    }
                }
            }

            var removals = projectItems2Delete.ToArray();

            for (int i = 0; i < removals.Length; i++)
                removals[i].Remove();
        }

        protected virtual void HandleRenamedFiles(Project project, Dictionary<string, string> fileName)
        {
            var it = new ProjectItemIterator(new[] { project }).GetEnumerator();

            while (it.MoveNext())
            {
                var item = it.Current;

                if (!string.IsNullOrEmpty(item.Name))
                {
                    if (fileName.Keys.Any(n => n.ToUpper() == item.Name.ToUpper()))
                    {
                        item.Name = fileName[item.Name];

                    }
                }
            }
        }

        protected virtual XElement CreateNuGetPackageNode(string packageId)
        {
            return CreateNuGetPackageNode(packageId, M2Consts.M2RunTimeVersion);
        }

        protected virtual XElement CreateNuGetPackageNode(string packageId, string version)
        {
            // hack, we know, will fix later
            if (packageId.ToLower().Contains("microsoft.sharepointonline.csom"))
            {
                return new XElement("package",
                  new XAttribute("id", packageId),
                  new XAttribute("version", "16.1.3912.1204")
                  );
            }

            return new XElement("package",
                new XAttribute("id", packageId),
                new XAttribute("version", version)
                );
        }

        protected virtual void HandleNuGetPackagesUpdate(string vsDefPath, List<string> packageIds)
        {
            var packages = packageIds.Select(p => CreateNuGetPackageNode(p));

            var xDoc = XDocument.Parse(File.ReadAllText(vsDefPath));
            var wizardDataNode =
                xDoc.Root.Descendants().FirstOrDefault(e => e.Name.LocalName.ToUpper() == "WizardData".ToUpper());

            if (wizardDataNode == null)
                xDoc.Root.Add(new XElement("WizardData", new XElement("packages", packages)));
            else
            {
                wizardDataNode.RemoveAll();
                wizardDataNode.Add(new XElement("packages", packages));
            }

            File.WriteAllText(vsDefPath, xDoc.ToString());
        }
    }
}
