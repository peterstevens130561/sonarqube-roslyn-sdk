﻿using Roslyn.SonarQube.Common;
using System;
using System.Collections.Generic;
using System.IO;

namespace Roslyn.SonarQube.PluginGenerator
{
    public class RulesPluginGenerator
    {
        private readonly IJdkWrapper jdkWrapper;
        private readonly ILogger logger;

        public RulesPluginGenerator(ILogger logger)
            :this(new JdkWrapper(), logger)
        {
        }

        public RulesPluginGenerator(IJdkWrapper jdkWrapper, ILogger logger)
        {
            if (jdkWrapper == null)
            {
                throw new ArgumentNullException("param");
            }

            this.jdkWrapper = jdkWrapper;
            this.logger = logger;
        }

        public void GeneratePlugin(PluginDefinition definition, string rulesFilePath, string fullJarFilePath)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }
            if (string.IsNullOrWhiteSpace(rulesFilePath))
            {
                throw new ArgumentNullException("rulesFilePath");
            }
            if (string.IsNullOrWhiteSpace(fullJarFilePath))
            {
                throw new ArgumentNullException("fullJarFilePath");
            }

            if (!File.Exists(rulesFilePath))
            {
                throw new FileNotFoundException(UIResources.Gen_Error_RulesFileDoesNotExists, rulesFilePath);
            }
            if (!this.jdkWrapper.IsJdkInstalled())
            {
                throw new InvalidOperationException(UIResources.JarB_JDK_NotInstalled);
            }

            if (File.Exists(fullJarFilePath))
            {
                this.logger.LogWarning(UIResources.Gen_ExistingJarWillBeOvewritten);
            }

            ValidateDefinition(definition);

            // Temp folder which resources will be unpacked into
            string tempWorkingDir = Path.Combine(Path.GetTempPath(), "plugins", Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempWorkingDir);

            BuildPlugin(definition, rulesFilePath, fullJarFilePath, tempWorkingDir);
        }

        private static void ValidateDefinition(PluginDefinition definition)
        {
            // TODO
            CheckPropertyIsSet(definition.Language, "Language");

            CheckPropertyIsSet(definition.Key, "Key");
            CheckPropertyIsSet(definition.Name, "Name");
        }

        private static void CheckPropertyIsSet(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(string.Format(System.Globalization.CultureInfo.CurrentCulture,
                    UIResources.Error_MissingProperty, name));
            }
        }

        private void BuildPlugin(PluginDefinition definition, string rulesFilePath, string fullJarPath, string workingFolder)
        {
            PluginBuilder builder = new PluginBuilder(this.jdkWrapper, this.logger);

            // No additional jar files apart from the SonarQube API jar are required for this source

            // Generate the source files
            Dictionary<string, string> replacementMap = new Dictionary<string, string>();
            PopulateSourceFileReplacements(definition, replacementMap);
            SourceGenerator.CreateSourceFiles(typeof(RulesPluginGenerator).Assembly, "Roslyn.SonarQube.PluginGenerator.Resources.", workingFolder, replacementMap);

            // Add the source files
            foreach (string sourceFile in Directory.GetFiles(workingFolder, "*.java", SearchOption.AllDirectories))
            {
                builder.AddSourceFile(sourceFile);
            }

            // Add the rules file as a resource
            builder.AddResourceFile(rulesFilePath, "resources/rules.xml");
            builder.SetProperties(definition);
            builder.SetJarFilePath(fullJarPath);
            builder.SetProperty(WellKnownPluginProperties.Class, "myorg." + definition.Key + ".Plugin");

            builder.Build();
        }

        private static void PopulateSourceFileReplacements(PluginDefinition definition, IDictionary<string, string> replacementMap)
        {
            replacementMap.Add("[LANGUAGE]", definition.Language);
            replacementMap.Add("[PLUGIN_KEY]", definition.Key);
            replacementMap.Add("[PLUGIN_NAME]", definition.Name);
        }

    }
}