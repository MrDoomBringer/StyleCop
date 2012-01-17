﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StyleCopOptionsSettingsKey.cs" company="http://stylecop.codeplex.com">
//   MS-PL
// </copyright>
// <license>
//   This source code is subject to terms and conditions of the Microsoft 
//   Public License. A copy of the license can be found in the License.html 
//   file at the root of this distribution. If you cannot locate the  
//   Microsoft Public License, please send an email to dlr@microsoft.com. 
//   By using this source code in any fashion, you are agreeing to be bound 
//   by the terms of the Microsoft Public License. You must not remove this 
//   notice, or any other, from this software.
// </license>
// <summary>
//   Class to hold all of the Configurable options for this addin.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

extern alias JB;

namespace StyleCop.ReSharper61.Options
{
    #region Using Directives

    using System.IO;
    using System.Reflection;
    using System.Windows.Forms;

    using JetBrains.Application.Settings;

    using Microsoft.Win32;

    using StyleCop.ReSharper61.Core;

    #endregion

    /// <summary>
    /// Class to hold all of the Configurable options for this addin.
    /// </summary>
    [SettingsKey(typeof(Missing), "StyleCop Options")]
    public class StyleCopOptionsSettingsKey
    {
        #region Constants and Fields

        /// <summary>
        /// Set to true to always check for updates when Visual Studio starts.
        /// </summary>
        private bool alwaysCheckForUpdatesWhenVisualStudioStarts;
        
        /// <summary>
        /// Tracks whether we should check for updates.
        /// </summary>
        private bool automaticallyCheckForUpdates;

        /// <summary>
        /// The number of days between update checks.
        /// </summary>
        private int daysBetweenUpdateChecks;

        /// <summary>
        /// The value of the detected path for StyleCop.
        /// </summary>
        private string styleCopDetectedPath;

        /// <summary>
        /// Set to true when we've attempted to get the StyleCop path.
        /// </summary>
        private bool attemptedToGetStyleCopPath;

        #endregion
        
        #region Properties
        
        /// <summary>
        /// Gets or sets a value indicating whether AlwaysCheckForUpdatesWhenVisualStudioStarts.
        /// </summary>
        [SettingsEntry(false, "Always Check For Updates When Visual Studio Starts")]
        public bool AlwaysCheckForUpdatesWhenVisualStudioStarts
        {
            get
            {
                return this.alwaysCheckForUpdatesWhenVisualStudioStarts;
            }

            set
            {
                this.alwaysCheckForUpdatesWhenVisualStudioStarts = value;
                SetRegistry("AlwaysCheckForUpdatesWhenVisualStudioStarts", value, RegistryValueKind.DWord);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether we check for updates when plugin starts.
        /// </summary>
        [SettingsEntry(true, "Automatically Check For Updates")]
        public bool AutomaticallyCheckForUpdates
        {
            get
            {
                return this.automaticallyCheckForUpdates;
            }

            set
            {
                this.automaticallyCheckForUpdates = value;
                SetRegistry("AutomaticallyCheckForUpdates", value, RegistryValueKind.DWord);
            }
        }

        /// <summary>
        /// Gets or sets DashesCountInFileHeader.
        /// </summary>
        [SettingsEntry(116, "Dashes Count In File Header")]
        public int DashesCountInFileHeader { get; set; }

        /// <summary>
        /// Gets or sets DaysBetweenUpdateChecks.
        /// </summary>
        [SettingsEntry(7, "Days Between Update Checks")]
        public int DaysBetweenUpdateChecks
        {
            get
            {
                return this.daysBetweenUpdateChecks;
            }

            set
            {
                this.daysBetweenUpdateChecks = value;
                SetRegistry("DaysBetweenUpdateChecks", value, RegistryValueKind.DWord);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether descriptive text should be inserted into missing documentation headers.
        /// </summary>
        [SettingsEntry(true, "Insert Text Into Documentation")]
        public bool InsertTextIntoDocumentation { get; set; }

        /// <summary>
        /// Gets or sets the last update check date.
        /// </summary>
        [SettingsEntry("1900-01-01", "Last Update Check Date")]
        public string LastUpdateCheckDate { get; set; }

        /// <summary>
        /// Gets or sets the ParsingPerformance value. 9 means every time R# calls us, 8 means after 1 second, 7 means after 2 seconds, etc.
        /// </summary>
        /// <value>
        /// The performance value.
        /// </value>
        [SettingsEntry(7, "Parsing Performance")]
        public int ParsingPerformance { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the analysis executes as you type.
        /// </summary>
        [SettingsEntry(true, "Analysis Enabled")]
        public bool AnalysisEnabled { get; set; }
        
        /// <summary>
        /// Gets or sets the Specified Assembly Path.
        /// </summary>
        /// <value>
        /// The allow null attribute.
        /// </value>
        [SettingsEntry("", "Specified Assembly Path")]
        public string SpecifiedAssemblyPath { get; set; }

        /// <summary>
        /// Gets or sets the text for inserting suppressmessageattributes.
        /// </summary>
        [SettingsEntry("Reviewed. Suppression is OK here.", "Suppress StyleCop Attribute Justification Text")]
        public string SuppressStyleCopAttributeJustificationText { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to use exclude from style cop setting.
        /// </summary>
        [SettingsEntry(true, "Use Exclude From StyleCop Setting")]
        public bool UseExcludeFromStyleCopSetting { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether declaration comments should be multi line or single line.
        /// </summary>
        [SettingsEntry(false, "Use Single Line Declaration Comments")]
        public bool UseSingleLineDeclarationComments { get; set; }

        #endregion
        
        #region Methods

        /// <summary>
        /// Detects the style cop path.
        /// </summary>
        /// <returns>
        /// The path to the detected StyleCop assembly.
        /// </returns>
        public static string DetectStyleCopPath()
        {
            var assemblyPath = GetStyleCopPath();
            return StyleCopReferenceHelper.LocationValid(assemblyPath) ? assemblyPath : null;
        }

        /// <summary>
        /// Gets the assembly location.
        /// </summary>
        /// <returns>
        /// The path to the StyleCop assembly.
        /// </returns>
        public string GetAssemblyPath()
        {
            if (!this.attemptedToGetStyleCopPath)
            {
                this.attemptedToGetStyleCopPath = true;

                if (!string.IsNullOrEmpty(this.SpecifiedAssemblyPath))
                {
                    if (StyleCopReferenceHelper.LocationValid(this.SpecifiedAssemblyPath))
                    {
                        this.styleCopDetectedPath = this.SpecifiedAssemblyPath;
                        return this.styleCopDetectedPath;
                    }

                    // Location not valid. Blank it and automatically get location
                    this.SpecifiedAssemblyPath = null;
                }

                this.styleCopDetectedPath = DetectStyleCopPath();

                if (string.IsNullOrEmpty(this.styleCopDetectedPath))
                {
                    MessageBox.Show(
                        string.Format("Failed to find the StyleCop Assembly. Please check your StyleCop installation."), "Error Finding StyleCop Assembly", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            return this.styleCopDetectedPath;
        }

        /// <summary>
        /// Gets the StyleCop assembly path.
        /// </summary>
        /// <returns>
        /// The path to the StyleCop assembly or null if not found.
        /// </returns>
        private static string GetStyleCopPath()
        {
            var directory = RetrieveFromRegistry() ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            return directory == null ? directory : Path.Combine(directory, StyleCopReferenceHelper.StyleCopAssemblyName);
        }

        /// <summary>
        /// Gets the StyleCop install location from the registry. This reg key is created by StyleCop during install.
        /// </summary>
        /// <returns>
        /// Returns the regkey value or null if not found.
        /// </returns>
        private static string RetrieveFromRegistry()
        {
            const string SubKey = @"SOFTWARE\CodePlex\StyleCop";
            const string Key = "InstallDir";

            RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(SubKey);
            return registryKey == null ? null : registryKey.GetValue(Key) as string;
        }
        
        /// <summary>
        /// Sets a regkey value in the registry.
        /// </summary>
        /// <param name="key">
        /// The subkey to create.
        /// </param>
        /// <param name="value">
        /// The value to use.
        /// </param>
        /// <param name="valueKind">
        /// The type of regkey value to set.
        /// </param>
        private static void SetRegistry(string key, object value, RegistryValueKind valueKind)
        {
            const string SubKey = @"SOFTWARE\CodePlex\StyleCop";

            var registryKey = Registry.CurrentUser.CreateSubKey(SubKey);
            if (registryKey != null)
            {
                registryKey.SetValue(key, value, valueKind);
            }
        }
        
        #endregion
    }
}