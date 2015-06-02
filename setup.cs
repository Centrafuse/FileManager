using System;
using System.IO;
using System.Xml;
using System.Web;
using System.Windows.Forms;
using centrafuse.Plugins;

namespace centrafuse.Plugins.FileManager
{
    class setup : CFSetup
    {

#region Variables

        private XmlDocument configxml = new XmlDocument();
        private XmlDocument languagexml = new XmlDocument();

#endregion

#region Construction

        public setup(ICFMain mForm, ConfigReader config, LanguageReader lang)
        {
            this.MainForm = mForm;

            this.pluginConfig = config;
            this.pluginLang = lang;

            CF_initSetup(1, 1);

            this.CF_updateText("TITLE", this.pluginLang.ReadField("/APPLANG/SETUP/HEADER"));
        }

#endregion

#region CFSetup

        public override void CF_setupReadSettings(int currentpage, bool advanced)
        {
            try
            {
                int i = CFSetupButton.One;

                switch (currentpage)
                {
                    case 1:

                        // TEXT BUTTONS (1-4)

                        ButtonHandler[i] = new CFSetupHandler(SetDisplayName);
                        ButtonText[i] = LanguageReader.GetText("/APPLANG/SETUP/DISPLAYNAME");
                        ButtonValue[i++] = pluginLang.ReadField("/APPLANG/FILEMANAGER/DISPLAYNAME");

                        ButtonHandler[i] = new CFSetupHandler(SetStartDir);
                        ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/LABEL2");
                        ButtonValue[i++] = pluginConfig.ReadField("/APPCONFIG/STARTDIR");

                        ButtonHandler[i] = null;
                        ButtonText[i] = "";
                        ButtonValue[i++] = "";

                        ButtonHandler[i] = null;
                        ButtonText[i] = "";
                        ButtonValue[i++] = "";

                        // BOOL BUTTONS (5-8)

                        ButtonHandler[i] = new CFSetupHandler(SetShowHidden);
                        ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/LABEL5");
                        ButtonValue[i++] = pluginConfig.ReadField("/APPCONFIG/SHOWHIDDEN");

                        ButtonHandler[i] = new CFSetupHandler(SetShowSystem);
                        ButtonText[i] = this.pluginLang.ReadField("/APPLANG/SETUP/LABEL6");
                        ButtonValue[i++] = pluginConfig.ReadField("/APPCONFIG/SHOWSYSTEM");

                        ButtonHandler[i] = null;
                        ButtonText[i] = "";
                        ButtonValue[i++] = "";

                        ButtonHandler[i] = null;
                        ButtonText[i] = "";
                        ButtonValue[i++] = "";

                        break;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

#endregion

#region Button Clicks

        private void SetDisplayName(ref object value)
        {
            try
            {
                string result = OSKDialog(LanguageReader.GetText("/APPLANG/SETUP/ENTERDISPLAYNAME"), ButtonValue[(int)value]);
                pluginLang.WriteField("/APPLANG/FILEMANAGER/DISPLAYNAME", result);
                ButtonValue[(int)value] = result;
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }


        private void SetStartDir(ref object value)
        {
            try
            {
                string result = OSKDialog(this.pluginLang.ReadField("/APPLANG/SETUP/BUTTON2TEXT"), this.CF_getButtonText("BUTTON2"));
                if (Directory.Exists(result))
                {
                    pluginConfig.WriteField("/APPCONFIG/STARTDIR", result);
                    ButtonValue[(int)value] = result;
                }
                else
                {
                    informationDialog(this.pluginLang.ReadField("/APPLANG/SETUP/FILENOTFOUND"));
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        private void SetShowSystem(ref object value)
        {
            this.pluginConfig.WriteField("/APPCONFIG/SHOWSYSTEM", value.ToString());
        }

        private void SetShowHidden(ref object value)
        {
            this.pluginConfig.WriteField("/APPCONFIG/SHOWHIDDEN", value.ToString());
        }

#endregion

#region Dialog Boxes

        /// <summary>
        /// Displays a Confirmation Dialog
        /// </summary>
        /// <param name="confirmationMessage">Confirmation Message</param>
        /// <returns>User Response</returns>
        private bool confirmationDialog(String confirmationMessage)
        {
            try
            {
                // Display Confirmation Dialog
                return this.CF_systemDisplayDialog(CF_Dialogs.YesNo, confirmationMessage) == DialogResult.OK;
            }
            catch (Exception errmsg)
            {
                CFTools.writeError(errmsg.Message, errmsg.StackTrace);
                return false;
            }
        }


        /// <summary>
        /// Displays an On Screen Keyboard Dialog
        /// </summary>
        /// <param name="requestMessage">message displayed on OSK screen</param>
        /// <param name="originalText">original text to be edited</param>
        /// <returns></returns>
        private string OSKDialog(string requestMessage, string originalText)
        {
            string resultValue = originalText;
            string resultText;
            try
            {
                // Displays OSK but sets the resultvalue back to the original text if the user cancels
                if (this.CF_systemDisplayDialog(CF_Dialogs.OSK, requestMessage, originalText, out resultValue, out resultText) != DialogResult.OK)
                {
                    resultValue = originalText;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
            return resultValue;
        }


        /// <summary>
        /// Displays an On Screen Keyboard Dialog
        /// </summary>
        /// <param name="requestMessage">message displayed on OSK screen</param>
        /// <param name="originalText">original text to be edited</param>
        /// <returns></returns>
        private bool ListDialog(string requestMessage, string originalText, string originalValue, out string resultValue, out string resultText, CFControls.CFListViewItem[] itemArray)
        {
            bool result = false;
            string value = string.Empty;
            string text = string.Empty;
            try
            {
                // Displays OSK but sets the resultvalue back to the original text if the user cancels
                if (this.CF_systemDisplayDialog(CF_Dialogs.FileBrowser, requestMessage, originalText, originalValue, out value, out text, itemArray) == DialogResult.OK)
                {
                    result = true;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
            resultText = text;
            resultValue = value;
            return result;
        }


        /// <summary>
        /// Displays an OK Dialog Box
        /// </summary>
        /// <param name="informationMessage">Message to be displayed</param>
        private void informationDialog(string informationMessage)
        {
            try
            {
                // Display the information message
                this.CF_systemDisplayDialog(CF_Dialogs.OkBox, informationMessage);
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

#endregion

    }
}