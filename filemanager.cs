using System;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Drawing;
using System.Collections;
using centrafuse.Plugins;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Data;

using CFControlsExtender.Base;
using CFControlsExtender.Imaging;
using CFControlsExtender.ItemsBuilder;
using CFControlsExtender.Listview;

namespace centrafuse.Plugins.FileManager 
{
    public class filemanager : CFPlugin
    {

#region Variables

        private setup mySetup;

        private string folderPath = string.Empty;
        private FileInfo copyFile;
        private DirectoryInfo copyDirectory;
        private bool isFileBeingCopied = false;
        private bool move = false;
        private int copycancel = 0;

        private bool atroot = false;
        private bool showHidden = false;
        private bool showSystem = false;

        private bool isCopyInProgress = false;

        private  CFControls.CFAdvancedList listFiles;
        private BindingSource fileBindingSource;
        private DataTable dtFiles;
        private System.Windows.Forms.Timer pagingTimer;
        private CFControls.CFListView.PagingDirection pagingDirection = CFControls.CFListView.PagingDirection.DOWN;

        private long copySize = 0;
        private long copyProgress = 0;
        private long lastcopyProgress = 0;
        private long lastTotalBytesTransferred = 0;
        private float progressFractionCurrent = 0;

        public static string localpath = CFTools.AppDataPath + "\\Plugins\\FileManager";
        public static string configpath = localpath + "\\config.xml";

        public const string PLUGIN_FOLDER = "Plugins/FileManager/";
        public const string VERSION = "v1.0";

        private string CurrentSkinFolder
        {
            get
            {
                return PLUGIN_FOLDER + "skins/" + this.CF_params.pluginSkin;
            }
        }

        #endregion

#region Constructor

        public filemanager()
        {
            this.CF_params.pauseAudio = false;
            this.CF_params.isGUI = true;
            this.CF_params.supportsRearScreen = true;
        }

#endregion

#region CFPlugin

        public override void CF_pluginInit()
        {
            try
            {
                this.CF_params.supportsRearScreen = true;

                this.CF3_initPlugin("FileManager", true);
                this.CF_params.settingsDisplayName = this.pluginLang.ReadField("/APPLANG/FILEMANAGER/TITLE");
                this.CF_params.settingsDisplayDesc = this.pluginLang.ReadField("/APPLANG/FILEMANAGER/DESCRIPTION");
                
                LoadSettings();
                
                this.fileBindingSource = new BindingSource();
                
                dtFiles = new DataTable("Files");
                dtFiles.Columns.Add("DisplayName", System.Type.GetType("System.String"));
                dtFiles.Columns.Add("Path", System.Type.GetType("System.String"));
                dtFiles.Columns.Add("isdir", System.Type.GetType("System.Boolean"));

                this.pagingTimer = new System.Windows.Forms.Timer();
                this.pagingTimer.Interval = 850;
                this.pagingTimer.Enabled = false;
                this.pagingTimer.Tick += new EventHandler(pagingTimer_Tick);

                this.CF_localskinsetup();
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }


        public override void CF_localskinsetup()
        {
            try
            {
                this.CF3_initSection("FileManager");

                if (CF_displayHooks.loadControl.advlistviews)
                {
                    this.listFiles = this.advancedlistArray[CF_getAdvancedListID("MAINPANEL")];
                    this.listFiles.DoubleClickListTiming = true;

                    if (this.listFiles != null)
                    {
                        this.listFiles.Backward += new EventHandler<EventArgs>(back_Click);
                        this.listFiles.Forward += new EventHandler<EventArgs>(forward_Click);
                        this.listFiles.DoubleClick += new EventHandler<ItemArgs>(listBox_DoubleClick);
                        this.listFiles.SelectedIndexChanged += new EventHandler<ItemArgs>(listBox_SelectedIndexChanged);
                        this.listFiles.LinkedItemClick += new EventHandler<LinkedItemArgs>(listFiles_LinkedItemClick);
                        this.listFiles.DataBinding = fileBindingSource;
                        this.fileBindingSource.DataSource = this.dtFiles;
                        this.listFiles.TemplateID = "default";
                        this.listFiles.Refresh();
                    }
                }
                else
                {
                    if (this.listFiles != null)
                    {
                        Rectangle rect = SkinReader.ParseBounds(SkinReader.GetControlAttribute("FileBrowser", "MainPanel", "bounds", null));
                        string templatefilemediamanageralbumfile = CFTools.StartupPath + "\\Skins\\" + SkinReader.currentSkin + "\\" + SkinReader.GetControlAttribute("MediaManager", "MainPanel", "templatefilemediamanageralbumfile", null).Replace("/", "\\");
                        string templatefilemediamanagersongfile = CFTools.StartupPath + "\\Skins\\" + SkinReader.currentSkin + "\\" + SkinReader.GetControlAttribute("MediaManager", "MainPanel", "templatefilemediamanagersongfile", null).Replace("/", "\\");

                        this.listFiles.TemplateID = "default";
#if !WindowsCE
                        this.listFiles.ScaleRatio = new PointF((float)SkinReader.CFDimensions.widthRatio, (float)SkinReader.CFDimensions.heightRatio);
#else
                        this.musicList.ScaleRatio = new System.Drawing.RectangleF((float)SkinReader.CFDimensions.widthRatio, (float)SkinReader.CFDimensions.heightRatio, 0, 0);
#endif
                        this.listFiles.Bounds = new Rectangle(((int)(rect.X * SkinReader.CFDimensions.widthRatio)),
                            ((int)(rect.Y * (int)SkinReader.CFDimensions.heightRatio)),
                            ((int)(rect.Width * SkinReader.CFDimensions.widthRatio)),
                            ((int)(rect.Height * SkinReader.CFDimensions.heightRatio)));

                        this.listFiles.UpdateBackground(this);
                    }
                }

                //CF_displayHooks.loadControl.advlistviews = false;
                //CF_displayHooks.clearControl.advlistviews = false;

                if (copyFile != null || copyDirectory != null)
                {
                    this.CF_setButtonOn("Centrafuse.FileManager.Copy");
                }

                // reload lang specific settings
                this.CF_params.displayName = this.pluginLang.ReadField("/APPLANG/FILEMANAGER/DISPLAYNAME");
                this.CF_params.settingsDisplayName = this.pluginLang.ReadField("/APPLANG/FILEMANAGER/DISPLAYNAME");

                if (!showFiles(false))
                {
                    // directory doesnt exist anymore, reset to default directory
                    folderPath = string.Empty;
                    showFiles(false);
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }


        public override bool CF_pluginCMLCommand(string command, string[] strparams, CF_ButtonState state, int zone)
        {
            CFTools.writeLog("FILEMANAGER", "CF_pluginCMLCommand", "action = " + command + ", state = " + state);

            try
            {
                command = command.ToLower().Replace("centrafuse.", "").Trim();

                switch (command.ToLower())
                {
                    case "filemanager.close":
                        if (state >= CF_ButtonState.Click)
                            close_Click();
                        return true;
                    case "filemanager.back":
                        if (state >= CF_ButtonState.Click)
                            back_Click();
                        return true;
                    case "filemanager.forward":
                        if (state >= CF_ButtonState.Click)
                            forward_Click();
                        return true;
                    case "filemanager.address":
                        if (state >= CF_ButtonState.Click)
                            address_Click();
                        return true;
                    case "filemanager.cut":
                        if (state >= CF_ButtonState.Click)
                            cut_Click();
                        return true;
                    case "filemanager.copy":
                        if (state >= CF_ButtonState.Click)
                            copy_Click();
                        return true;
                    case "filemanager.paste":
                        if (state >= CF_ButtonState.Click)
                            paste_Click();
                        return true;
                    case "filemanager.delete":
                        if (state >= CF_ButtonState.Click)
                            delete_Click();
                        return true;
                    case "filemanager.create":
                        if (state >= CF_ButtonState.Click)
                            createFolder_Click();
                        return true;
                    case "filemanager.rename":
                        if (state >= CF_ButtonState.Click)
                            rename_Click();
                        return true;
                    case "filemanager.pageup":
                        if (state == CF_ButtonState.Down)
                        {
                            page_up_Click();
                            pagingTimer.Enabled = true;
                        }
                        else if (state == CF_ButtonState.Click)
                            pagingTimer.Enabled = false;
                        return true;
                    case "filemanager.pagedown":
                        if (state == CF_ButtonState.Down)
                        {
                            page_down_Click();
                            pagingTimer.Enabled = true;
                        }
                        else if (state == CF_ButtonState.Click)
                            pagingTimer.Enabled = false;
                        return true;
                }
            }
            catch { }

            return false;
        }


        public override void CF_pluginShow()
        {
            this.Visible = true;
        }


        public override DialogResult CF_pluginShowSetup()
        {
            DialogResult returnvalue = DialogResult.Cancel;

            try
            {
                mySetup = new setup(this.MainForm, this.pluginConfig, this.pluginLang);
                returnvalue = mySetup.ShowDialog();
                if (returnvalue == DialogResult.OK)
                {
                    LoadSettings();
                }
                mySetup.Close();
                mySetup = null;
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }

            return returnvalue;
        }


        public override void CF_pluginCommand(string command, string param1, string param2)
        {
        }

        
        public override string CF_pluginData(string command, string param)
        {
            string retvalue = "";
            return retvalue;
        }


        public void LoadSettings()
        {
            try
            {
                this.CF_params.displayName = this.pluginLang.ReadField("/APPLANG/FILEMANAGER/DISPLAYNAME");

                if (folderPath.Equals(string.Empty))
                {
                    this.folderPath = this.pluginConfig.ReadField("/APPCONFIG/STARTDIR");
                }
                if (!folderPath.EndsWith("\\"))
                {
                    folderPath = folderPath + "\\";
                }

                if (this.pluginConfig.ReadField("/APPCONFIG/SHOWHIDDEN").Equals(true.ToString()))
                    showHidden = true;
                else
                    showHidden = false;

                if (this.pluginConfig.ReadField("/APPCONFIG/SHOWSYSTEM").Equals(true.ToString()))
                    showSystem = true;
                else
                    showSystem = false;
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }


        public override string CF_pluginDefaultConfig()
        {
            return "<APPCONFIG>\r\n" +
                    "    <SKIN>Clean</SKIN>\r\n" +
                    "    <APPLANG>English</APPLANG>\r\n" +
                    "    <STARTDIR></STARTDIR>\r\n" +
                    "    <SHOWHIDDEN>False</SHOWHIDDEN>\r\n" +
                    "    <SHOWSYSTEM>False</SHOWSYSTEM>\r\n" +
                    "</APPCONFIG>\r\n";
        }

#endregion

#region Click Events / Widget Events

        private void listFiles_LinkedItemClick(object sender, LinkedItemArgs e)
        {
            try
            {
                switch (e.LinkId.ToLower())
                {
                    case "info":
                        displayFileInfo();
                        break;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        private void listBox_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                CFTools.writeLog("TEST1");

                if (listFiles.SelectedItems.Count <= 0)
                    return;     // Do nothing, though this should not happen

                CFTools.writeLog("TEST2");

                string path = GetSelectedPath();
                if (File.Exists(path))
                {
                    CFTools.writeLog("TEST3");

                    Process proc = new Process();
                    proc.EnableRaisingEvents = false;
                    proc.StartInfo.FileName = path;
                    proc.Start();
                }
                if (Directory.Exists(path) || path.Equals(string.Empty))
                {
                    CFTools.writeLog("TEST4");

                    folderPath = path;
                    showFiles(false);
                }

                CFTools.writeLog("TEST5");
            }
            catch (Exception ex)
            {
                informationDialog(ex.Message);
            }
        }

        private void address_Click()
        {
            try
            {
                string folder = OSKDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/ENTERDIRECTORY"), folderPath);
                if (!Directory.Exists(folder))
                {
                    informationDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/INVALIDPATH"));
                }
                else if (!folder.Equals(folderPath))
                {
                    folderPath = folder;
                    showFiles(false);
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        private void cut_Click()
        {
            try
            {
                if (setFileToCopy(true))
                {
                    CF_setButtonOn("Centrafuse.FileManager.Cut");
                    CF_setButtonOff("Centrafuse.FileManager.Copy");
                }
            }
            catch (Exception ex)
            {
                informationDialog(ex.Message);
            }
        }

        private void copy_Click()
        {
            try
            {
                if (setFileToCopy(false))
                {
                    CF_setButtonOn("Centrafuse.FileManager.Copy");
                    CF_setButtonOff("Centrafuse.FileManager.Cut");
                }
            }
            catch (Exception ex)
            {
                informationDialog(ex.Message);
            }
        }

        private void paste_Click()
        {
            if (copyFile != null || copyDirectory != null)
            {
                startCopy();
            }
        }

        private void delete_Click()
        {
            try
            {
                if (listFiles.SelectedItems.Count <= 0)
                    return;     // Do nothing, though this should not happen

                string strSelected = GetSelectedPath();
                if (strSelected == "")
                    return;

                if (Directory.Exists(strSelected))
                {
                    if (confirmationDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/DELETEFOLDER")))
                    {
                        Directory.Delete(strSelected, true);
                        showFiles(true);
                    }
                }
                else
                {
                    if (confirmationDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/DELETEFILE")))
                    {
                        File.Delete(strSelected);
                        showFiles(true);
                    }
                }
            }
            catch (Exception ex)
            {
                informationDialog(ex.Message);
            }
        }

        private void page_up_Click()
        {
            pagingDirection = CFControls.CFListView.PagingDirection.UP;
            this.listFiles.PageUp();
        }

        private void page_down_Click()
        {
            pagingDirection = CFControls.CFListView.PagingDirection.DOWN;
            this.listFiles.PageDown();
        }

        void listBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            updatePages();
        }

        private void close_Click()
        {
            try
            {
                if (this.CF_displayHooks.rearScreen)
                    this.CF_systemCommand(CF_Actions.EXTAPPCLOSE, "REARSCREEN");
                else if (Screen.AllScreens.Length >= CF_displayHooks.displayNumber && CF_displayHooks.displayNumber > 1)
                    this.CF_systemCommand(CF_Actions.EXTAPPCLOSE, "FALSE", "FALSE");
                else
                    this.CF_systemCommand(CF_Actions.EXTAPPCLOSE);

                //this.Visible = false;
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }

            return;
        }

        private void back_Click(object sender, EventArgs e)
        {
            back_Click();
        }

        private void back_Click()
        {
            try
            {
                if (!atroot)
                {
                    //don't like this solution, but it works
                    listFiles.SelectedItems.Clear();
                    listFiles.SelectedItems.Add(0);
                    this.listFiles.Update();

                    listBox_DoubleClick(null, null);
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }

            return;
        }

        private void forward_Click(object sender, EventArgs e)
        {
            forward_Click();
        }

        private void forward_Click()
        {
            listBox_DoubleClick(null, null);
            return;
        }

        private void createFolder_Click()
        {
            try
            {
                string result = OSKDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/CREATEDIRECTORY"), string.Empty);
                if (result != string.Empty)
                {
                    string newDir = folderPath.TrimEnd('\\') + "\\" + result;
                    if (Directory.Exists(newDir))
                    {
                        informationDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/DIRECTORYEXISTS"));
                    }
                    else
                    {
                        try
                        {
                            Directory.CreateDirectory(newDir);
                            showFiles(true);
                        }
                        catch (Exception Exception)
                        {
                            informationDialog(Exception.Message);
                        }
                    }
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        private void rename_Click()
        {
            try
            {
                if (listFiles.SelectedItems.Count <= 0)
                    return;     // Do nothing, though this should not happen

                string strSelected = GetSelectedPath();
                if (strSelected == "")
                    return;

                if (!strSelected.Equals(".."))
                {
                    string currentPath = strSelected;
                    if (File.Exists(currentPath))
                    {
                        FileInfo currentFile = new FileInfo(currentPath);
                        string result = OSKDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/RENAMEFILE"), currentFile.Name);
                        if (result != string.Empty && !result.Equals(currentFile.Name))
                        {
                            try
                            {
                                string folder = currentFile.Directory.FullName;
                                string destination = folder.TrimEnd('\\') + "\\" + result;
                                if (File.Exists(destination))
                                {
                                    informationDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/FILEEXISTS"));
                                }
                                else
                                {
                                    currentFile.MoveTo(destination);
                                    showFiles(true);
                                }
                            }
                            catch (Exception Exception)
                            {
                                informationDialog(Exception.Message);
                            }
                        }
                    }
                    else if (Directory.Exists(currentPath))
                    {
                        DirectoryInfo currentFolder = new DirectoryInfo(currentPath);
                        string result = OSKDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/RENAMEFOLDER"), currentFolder.Name);
                        if (result != string.Empty && !result.Equals(currentFolder.Name))
                        {
                            try
                            {
                                string folder = currentFolder.Parent.FullName;
                                string destination = folder.TrimEnd('\\') + "\\" + result;
                                if (Directory.Exists(destination))
                                {
                                    informationDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/DIRECTORYEXISTS"));
                                }
                                else
                                {
                                    currentFolder.MoveTo(destination);
                                    showFiles(true);
                                }
                            }
                            catch (Exception Exception)
                            {
                                informationDialog(Exception.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

#endregion

#region Copy

        private bool setFileToCopy(bool moveFile)
        {
            bool result = false;

            try
            {
                if (listFiles.SelectedItems.Count <= 0)
                    return false;     // Do nothing

                string strSelected = GetSelectedPath();
                if (strSelected == "")
                    return false;

                if (File.Exists(strSelected))
                {
                    isFileBeingCopied = true;
                    copyFile = new FileInfo(strSelected);
                    result = true;
                }
                else if (Directory.Exists(strSelected))
                {
                    isFileBeingCopied = false;
                    copyDirectory = new DirectoryInfo(strSelected);
                    result = true;
                }
                move = moveFile;
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }

            return result;
        }

        public void copyWholeDirectory(string sourceFolder, string targetFolder)
        {
            if (this.copycancel != 0)
                return;

            try
            {
                DirectoryInfo sourceDir = new DirectoryInfo(sourceFolder);
                DirectoryInfo[] dirs = sourceDir.GetDirectories();
                FileInfo[] files = sourceDir.GetFiles();

                foreach (DirectoryInfo dir in dirs)
                {
                    if (this.copycancel != 0)
                        return;
                    string newDir = targetFolder + "\\" + dir.Name;
                    if (!Directory.Exists(newDir))
                    {
                        Directory.CreateDirectory(newDir);
                    }
                    copyWholeDirectory(dir.FullName, newDir);
                    if (this.copycancel != 0)
                        return;
                }

                foreach (FileInfo file in files)
                {
                    string newFile = targetFolder + "\\" + file.Name;
                    this.lastTotalBytesTransferred = 0;
                    Win32.CopyFileEx(file.FullName, newFile, new Win32.CopyProgressRoutine(CopyProgress), 0, ref copycancel, 0);
                    if (this.copycancel != 0)
                        break;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }


        private void FileManager_statusClick(object sender, MouseEventArgs m)
        {
            this.copycancel = 1;
        }

        private void updateCopyStatus()
        {
            try
            {
                if (copySize != 0)
                {
                    float progressFraction = (float)((float)copyProgress / (float)copySize);
                    if (progressFraction > progressFractionCurrent + 0.001)
                    {
                        this.CF_updateText("ProgressTxt", byteSizeToString(copyProgress) + " / " + byteSizeToString(copySize));
                        Bitmap progressBarBmp = new Bitmap(376, 28, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                        Graphics graphics = Graphics.FromImage(progressBarBmp);
                        graphics.Clear(Color.Transparent);
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        graphics.FillRectangle(Brushes.LightBlue, 0f, 0f, progressFraction * 376, 28);
                        graphics.Flush();
                        graphics.Dispose();
                        this.CF_setPictureImage("Progress", progressBarBmp);
                        progressFractionCurrent = progressFraction;
                    }
                }

                string message = string.Empty;
                if (move)
                {
                    message = "MOVING ";
                }
                else
                {
                    message = "COPYING ";
                }

                if (isFileBeingCopied)
                {
                    this.CF_updateText("Status", message + copyFile.Name);
                }
                else
                {
                    this.CF_updateText("Status", message + copyDirectory.Name);
                }
            }
            catch (Exception ex) 
            {
                informationDialog(ex.Message);
            }
        }

        private void startCopy()
        {
            if (isCopyInProgress)
                return;

            isCopyInProgress = true;
          //  executeCopy2();
            executeCopy();
        }


        private uint CopyProgress(long TotalFileSize, long TotalBytesTransferred, long StreamSize, long StreamBytesTransferred, int dwStreamNumber,int dwCallbackReason, IntPtr hSourceFile, IntPtr hDestinationFile, int lpData)
        {
            try
            {
                long thistransfer = TotalBytesTransferred - lastTotalBytesTransferred;
                copyProgress += thistransfer;
                lastTotalBytesTransferred = TotalBytesTransferred;
                if (copyProgress > lastcopyProgress + 1024)
                {
                    updateCopyStatus();
                    lastcopyProgress = copyProgress;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }

            return 0;
        }

        private void executeCopy2()
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    if (isFileBeingCopied && copyFile != null && copyFile.Exists)
                    {
                        string targetFolder = folderPath.TrimEnd('\\') + "\\";
                        string newFile = targetFolder + copyFile.Name;
                        if (!move && copyFile.DirectoryName.Equals(folderPath))
                        {
                            string part1 = newFile.Remove(newFile.LastIndexOf('.'));
                            string part2 = newFile.Substring(newFile.LastIndexOf('.'));
                            bool done = false;
                            int counter = 1;
                            while (!done)
                            {
                                string newFileName = part1 + "(" + counter.ToString() + ")" + part2;
                                if (!File.Exists(newFileName))
                                {
                                    newFile = newFileName;
                                    done = true;
                                }
                                counter++;
                            }
                        }
                        if (!File.Exists(newFile) || confirmationDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/OVERWRITEFILE") + " " + copyFile.Name))
                        {
                            object resultobject = new object();
                            string resultvalue = "", resulttext = "";
                            DialogResult dr = CF_systemDisplayDialog(CF_Dialogs.Copier, LanguageReader.GetText("APPLANG/DIALOGTEXT/COPIED"), copyFile.FullName, newFile, out resultvalue, out resulttext, out resultobject, null, false, false, false, false, false, false, this.CF_displayHooks.displayNumber);
                            if (dr == DialogResult.OK && move)
                            {
                                copyFile.Delete();
                                copyFile = null;
                            }
                        }
                    }
                    else if (!isFileBeingCopied && copyDirectory != null && copyDirectory.Exists)
                    {
                        string targetFolder = folderPath.TrimEnd('\\') + "\\" + copyDirectory.Name;

                        if (copyDirectory.Parent.FullName.Equals(folderPath))
                        {
                            bool done = false;
                            int counter = 1;
                            while (!done)
                            {
                                string newFolderName = targetFolder + "(" + counter.ToString() + ")";
                                if (!Directory.Exists(newFolderName))
                                {
                                    targetFolder = newFolderName;
                                    done = true;
                                }
                                counter++;
                            }
                        }

                        if (!Directory.Exists(targetFolder))
                            Directory.CreateDirectory(targetFolder);
                        else if (!confirmationDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/DIRECTORYALREADYEXISTS")))
                            return;

                        object resultobject = new object();
                        string resultvalue = "", resulttext = "";
                        DialogResult dr = CF_systemDisplayDialog(CF_Dialogs.Copier, LanguageReader.GetText("APPLANG/DIALOGTEXT/COPIED"), copyDirectory.FullName, targetFolder, out resultvalue, out resulttext, out resultobject, null, false, false, false, false, false, false, this.CF_displayHooks.displayNumber);
                        if (dr == DialogResult.OK && move)
                        {
                            Directory.Delete(copyDirectory.FullName);
                            copyDirectory = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                informationDialog(ex.Message);
            }

            isCopyInProgress = false;
            showFiles(false);
            CF_setButtonOff("Centrafuse.FileManager.Cut");
        }


        private void executeCopy()
        {
            this.copySize = 0;
            this.copyProgress = 0;
            this.copycancel = 0;
            this.lastcopyProgress = 0;
            this.lastTotalBytesTransferred = 0;
            this.progressFractionCurrent = 0;

            try
            {
                if (Directory.Exists(folderPath))
                {
                    if (isFileBeingCopied && copyFile != null && copyFile.Exists)
                    {
                        string targetFolder = folderPath.TrimEnd('\\') + "\\";
                        string newFile = targetFolder + copyFile.Name;
                        if (!move && copyFile.DirectoryName.Equals(folderPath))
                        {
                            string part1 = newFile.Remove(newFile.LastIndexOf('.'));
                            string part2 = newFile.Substring(newFile.LastIndexOf('.'));
                            bool done = false;
                            int counter = 1;
                            while (!done)
                            {
                                string newFileName = part1 + "(" + counter.ToString() + ")" + part2;
                                if (!File.Exists(newFileName))
                                {
                                    newFile = newFileName;
                                    done = true;
                                }
                                counter++;
                            }
                        }
                        if (!File.Exists(newFile) || confirmationDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/OVERWRITEFILE") + " " + copyFile.Name))
                        {
                            copySize = copyFile.Length;
                            updateCopyStatus();
                            if (move)
                            {
                                Win32.CopyFileEx(copyFile.FullName, newFile, new Win32.CopyProgressRoutine(CopyProgress), 0, ref copycancel, 0);
                                copyFile.Delete();
                                copyFile = null;
                            }
                            else
                            {
                                Win32.CopyFileEx(copyFile.FullName, newFile, new Win32.CopyProgressRoutine(CopyProgress), 0, ref copycancel, 0);
                            }
                            copyProgress = copySize;
                            updateCopyStatus();
                        }
                    }
                    else if (!isFileBeingCopied && copyDirectory != null && copyDirectory.Exists)
                    {
                        copySize = getFolderSize(copyDirectory);
                        updateCopyStatus();

                        if (move)
                        {
                            copyDirectory.MoveTo(folderPath.TrimEnd('\\') + "\\" + copyDirectory.Name);
                            copyProgress = copySize;
                            copyDirectory = null;
                        }
                        else
                        {
                            string targetFolder = folderPath.TrimEnd('\\') + "\\" + copyDirectory.Name;

                            if (copyDirectory.Parent.FullName.Equals(folderPath))
                            {
                                bool done = false;
                                int counter = 1;
                                while (!done)
                                {
                                    string newFolderName = targetFolder + "(" + counter.ToString() + ")";
                                    if (!Directory.Exists(newFolderName))
                                    {
                                        targetFolder = newFolderName;
                                        done = true;
                                    }
                                    counter++;
                                }
                            }

                            if (!Directory.Exists(targetFolder))
                            {
                                Directory.CreateDirectory(targetFolder);
                            }
                            else if (!confirmationDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/DIRECTORYALREADYEXISTS")))
                            {
                                return;
                            }
                            copyWholeDirectory(copyDirectory.FullName, targetFolder);
                        }
                        updateCopyStatus();
                    }
                }
            }
            catch (Exception ex)
            {
                informationDialog(ex.Message);
            }

            isCopyInProgress = false;
            this.CF_localskinsetup();
            CF_setButtonOff("Centrafuse.FileManager.Cut");
        }

        private long getFolderSize(DirectoryInfo copyDirectory)
        {
            long directorySize = 0;

            try
            {
                DirectoryInfo[] dirs = copyDirectory.GetDirectories();
                FileInfo[] files = copyDirectory.GetFiles();

                foreach (DirectoryInfo dir in dirs)
                {
                    directorySize += getFolderSize(dir);
                }

                foreach (FileInfo file in files)
                {
                    directorySize += file.Length;
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }

            return directorySize;
        }

#endregion

#region List

        private bool showFiles(bool keepSelectedIndex)
        {
            bool success = false;

            try
            {
                int select = 0;

                if (keepSelectedIndex)
                {
                    if (listFiles.SelectedItems.Count > 0)
                        select = listFiles.SelectedItems[0];
                }

                dtFiles.Rows.Clear();
                atroot = false;

                if (folderPath.Equals(string.Empty))
                {
                    atroot = true;
                    this.pluginConfig.WriteField("/APPCONFIG/STARTDIR", "", true);

                    this.CF_updateButtonText("ADDRESS", "Drive List");
                    DriveInfo[] drives = DriveInfo.GetDrives();
                    for (int i = 0; i < drives.Length; i++)
                    {
                        string driveString = drives[i].RootDirectory.FullName;
                        if (drives[i].IsReady)
                        {
                            string totalSize = byteSizeToString(drives[i].TotalSize);
                            string availableSize = byteSizeToString(drives[i].AvailableFreeSpace);

                            driveString += " [" + totalSize + " total, " + availableSize + " free]";
                        }

                        DataRow dr = dtFiles.NewRow();
                        dr[0] = driveString;
                        dr[1] = drives[i].RootDirectory.FullName;
                        dr[2] = true;
                        dtFiles.Rows.Add(dr);
                    }
                }
                else if (Directory.Exists(folderPath))
                {
                    string foldername = folderPath;

                    CFTools.writeLog("FILEMGR", "showFiles", "folderPath = " + folderPath + ", root = " + Path.GetPathRoot(folderPath));

                    if (!CFTools.IsUsbDrive(Path.GetPathRoot(folderPath)))
                    {
                        this.pluginConfig.WriteField("/APPCONFIG/STARTDIR", folderPath, true);
                    }
                    string[] folders = folderPath.TrimEnd('\\').Split('\\');
                    if (folders.Length > 1)
                    {
                        foldername = folders[folders.Length - 1];
                        DirectoryInfo dir = new DirectoryInfo(folderPath);

                        DataRow dr = dtFiles.NewRow();
                        dr[0] = "..";
                        dr[1] = dir.Parent.FullName;
                        dr[2] = true;
                        dtFiles.Rows.Add(dr);
                    }
                    else
                    {
                        DataRow dr = dtFiles.NewRow();
                        dr[0] = "..";
                        dr[1] = "";
                        dr[2] = true;
                        dtFiles.Rows.Add(dr);
                    }

                    this.CF_updateButtonText("ADDRESS", folderPath);

                    string[] dirs = Directory.GetDirectories(folderPath);
                    string[] files = Directory.GetFiles(folderPath);

                    for (int i = 0; i < dirs.Length; i++)
                    {
                        DirectoryInfo dir = new DirectoryInfo(dirs[i]);
                        if ((showHidden || !dir.Attributes.ToString().Contains(FileAttributes.Hidden.ToString())) &&
                            (showSystem || !dir.Attributes.ToString().Contains(FileAttributes.System.ToString())))
                        {
                            bool added = false;
                            for (int j = 1; j < dtFiles.Rows.Count; j++)
                            {
                                if (dtFiles.Rows[j]["DisplayName"].ToString().CompareTo(dir.Name) > 0)
                                {
                                    DataRow dr = dtFiles.NewRow();
                                    dr[0] = dir.Name;
                                    dr[1] = dir.FullName;
                                    dr[2] = true;
                                    dtFiles.Rows.Add(dr);
                                    added = true;
                                    break;
                                }
                            }

                            if (!added)
                            {
                                DataRow dr = dtFiles.NewRow();
                                dr[0] = dir.Name;
                                dr[1] = dir.FullName;
                                dr[2] = true;
                                dtFiles.Rows.Add(dr);
                            }
                        }
                    }

                    int fileStart = dtFiles.Rows.Count;
                    for (int i = 0; i < files.Length; i++)
                    {
                        FileInfo file = new FileInfo(files[i]);
                        if ((showHidden || !file.Attributes.ToString().Contains(FileAttributes.Hidden.ToString())) &&
                            (showSystem || !file.Attributes.ToString().Contains(FileAttributes.System.ToString())))
                        {
                            bool added = false;
                            for (int j = fileStart; j < dtFiles.Rows.Count; j++)
                            {
                                if (dtFiles.Rows[j]["DisplayName"].ToString().CompareTo(file.Name) > 0)
                                {
                                    DataRow dr = dtFiles.NewRow();
                                    dr[0] = file.Name;
                                    dr[1] = file.FullName;
                                    dr[2] = false;
                                    dtFiles.Rows.Add(dr);
                                    added = true;
                                    break;
                                }
                            }
                            if (!added)
                            {
                                DataRow dr = dtFiles.NewRow();
                                dr[0] = file.Name;
                                dr[1] = file.FullName;
                                dr[2] = false;
                                dtFiles.Rows.Add(dr);
                            }
                        }
                    }
                }
                else
                {
                    informationDialog(this.pluginLang.ReadField("/APPLANG/FILEMANAGER/INVALIDPATH"));
                    return success;
                }

                if (keepSelectedIndex && dtFiles.Rows.Count > 0)
                {
                    if (select < dtFiles.Rows.Count)
                        fileBindingSource.Position = select;
                    else
                        fileBindingSource.Position = dtFiles.Rows.Count - 1;
                }

                updatePages();
                this.listFiles.Refresh();

                success = true;
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }

            return success;
        }

        
        private static string byteSizeToString(long driveTotalSize)
        {
            string totalSize = string.Empty;

            try
            {
                if (driveTotalSize / 1024 < 1)
                {
                    totalSize = driveTotalSize.ToString() + " B";
                }
                else if ((driveTotalSize / 1024) / 1024 < 1)
                {
                    totalSize = Math.Round(driveTotalSize / 1024d, 2).ToString("F0") + " KB";
                }
                else if (((driveTotalSize / 1024) / 1024) / 1024 < 1)
                {
                    totalSize = Math.Round((driveTotalSize / 1024d) / 1024, 2).ToString("F1") + " MB";
                }
                else if ((((driveTotalSize / 1024) / 1024) / 1024) / 1024 < 1)
                {
                    totalSize = Math.Round(((driveTotalSize / 1024d) / 1024) / 1024, 2).ToString("F2") + " GB";
                }
                else
                {
                    totalSize = Math.Round((((driveTotalSize / 1024d) / 1024) / 1024) / 1024, 2).ToString("F3") + " TB";
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }

            return totalSize;
        }

        
        private void updatePages()
        {
            try
            {
                int atPage = fileBindingSource.Position + 1;
                int numberOfPages = fileBindingSource.Count;
                this.CF_updateText("Pages", atPage.ToString() + "/" + numberOfPages.ToString());
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

#endregion

#region Search

        private ArrayList searchDirectory(string folder, string searchParams)
        {
            ArrayList results = new ArrayList();

            try
            {
                if (Directory.Exists(folder))
                {
                    DirectoryInfo currentDir = new DirectoryInfo(folder);
                    results.AddRange(currentDir.GetFiles(searchParams));
                    results.AddRange(currentDir.GetDirectories(searchParams));
                    foreach (DirectoryInfo dir in currentDir.GetDirectories())
                    {
                        results.AddRange(searchDirectory(dir.FullName, searchParams));
                    }
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }

            return results;
        }

#endregion

#region External Application Launching Code

        public static ArrayList externalApplications;

        /// <summary>
        /// Checks the file extension
        /// </summary>
        /// <param name="ext"></param>
        /// <returns></returns>
        public static bool checkExtension(string ext)
        {
            bool retvalue = false;
            try
            {
                for (int i = 0; i < filemanager.externalApplications.Count; i++)
                {
                    string[] extarray = ((ExternalApplication)filemanager.externalApplications[i]).extensions.Split('|');
                    for (int a = 0; a < extarray.Length; a++)
                    {
                        if (extarray[a].ToUpper().Trim() == ext.ToUpper().Trim())
                        {
                            retvalue = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
            return retvalue;
        }

        private void loadApp(string ext, string filename)
        {
            try
            {
                for (int i = 0; i < filemanager.externalApplications.Count; i++)
                {
                    string[] extarray = ((ExternalApplication)filemanager.externalApplications[i]).extensions.Split('|');
                    for (int a = 0; a < extarray.Length; a++)
                    {
                        if (extarray[a].ToUpper().Trim() == ext.ToUpper().Trim())
                        {
                            ExternalApplication appToLoad = (ExternalApplication)filemanager.externalApplications[i];
                            //this.CF_loadExternalApplication(appToLoad.appdisplayname, appToLoad.apppausemusic, appToLoad.guiapplication, appToLoad.appinputdevice, appToLoad.appinputline, appToLoad.display, appToLoad.apppath, filename, appToLoad.appwindowname, appToLoad.appstartfullscreen);
                            break;
                        }
                    }
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }


        public void readExternalApps()
        {
            try
            {
                XmlDocument extdoc = new XmlDocument();
                extdoc.Load(CFTools.AppDataPath + "\\Plugins\\Email\\appattachments.xml");

                XmlNodeList appnodes = extdoc.SelectNodes("/APPATTACHMENTS/APPLICATION");
                filemanager.externalApplications = new ArrayList();
                foreach (XmlNode mynode in appnodes)
                {
                    try
                    {
                        string appdisplayname = System.Web.HttpUtility.HtmlDecode(mynode.SelectSingleNode("@APPNAME").InnerText);
                        string apppath = System.Web.HttpUtility.HtmlDecode(mynode.SelectSingleNode("PATH").InnerText);
                        string appwindowname = System.Web.HttpUtility.HtmlDecode(mynode.SelectSingleNode("WINDOWNAME").InnerText);
                        string appdisplay = System.Web.HttpUtility.HtmlDecode(mynode.SelectSingleNode("DISPLAY").InnerText);
                        string extensions = System.Web.HttpUtility.HtmlDecode(mynode.SelectSingleNode("EXTENSIONS").InnerText);
                        int display;
                        bool apppausemusic = false;
                        bool appstartfullscreen = false;
                        bool guiapplication = true;
                        try { display = Int32.Parse(System.Web.HttpUtility.HtmlDecode(mynode.SelectSingleNode("DISPLAY").InnerText)); }
                        catch { display = 1; }
                        try { apppausemusic = Boolean.Parse(System.Web.HttpUtility.HtmlDecode(mynode.SelectSingleNode("PAUSEMUSIC").InnerText)); }
                        catch { apppausemusic = false; }
                        try { appstartfullscreen = Boolean.Parse(System.Web.HttpUtility.HtmlDecode(mynode.SelectSingleNode("STARTFULLSCREEN").InnerText)); }
                        catch { appstartfullscreen = false; }
                        try { guiapplication = Boolean.Parse(System.Web.HttpUtility.HtmlDecode(mynode.SelectSingleNode("GUIAPPLICATION").InnerText)); }
                        catch { guiapplication = true; }
                        if (apppath != "" && (!guiapplication || (guiapplication && appwindowname != "")))
                        {
                            ExternalApplication newapplication = new ExternalApplication();
                            newapplication.appdisplayname = appdisplayname;
                            newapplication.apppausemusic = apppausemusic;
                            newapplication.guiapplication = guiapplication;
                            try
                            {
                                newapplication.appinputdevice = Int32.Parse(System.Web.HttpUtility.HtmlDecode(mynode.SelectSingleNode("LINECONTROL").InnerText).Split('|')[0]);
                                newapplication.appinputline = Int32.Parse(System.Web.HttpUtility.HtmlDecode(mynode.SelectSingleNode("LINECONTROL").InnerText).Split('|')[1]);
                            }
                            catch
                            {
                                mynode.SelectSingleNode("LINECONTROL").InnerText = "-1|-1";
                                extdoc.SelectSingleNode("/APPATTACHMENTS/APPLICATION[@APPNAME='" + mynode.Attributes["APPNAME"].Value + "']/LINECONTROL").InnerText = "-1|-1";
                                extdoc.Save(CFTools.AppDataPath + "\\Plugins\\Email\\appattachments.xml");
                                newapplication.appinputdevice = -1;
                                newapplication.appinputline = -1;
                            }
                            newapplication.display = display;
                            newapplication.apppath = apppath;
                            newapplication.appwindowname = appwindowname;
                            newapplication.appstartfullscreen = appstartfullscreen;
                            newapplication.extensions = extensions;
                            filemanager.externalApplications.Add(newapplication);
                        }
                    }
                    catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
                }
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
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

        private void displayFileInfo()
        {
            try
            {
                string path = GetSelectedPath();
                if (File.Exists(path))
                {
                    FileInfo file = new FileInfo(path);
                    string fileInformation = this.pluginLang.ReadField("/APPLANG/FILEMANAGER/SIZE") + " " + byteSizeToString(file.Length) + " " + Environment.NewLine
                        + this.pluginLang.ReadField("/APPLANG/FILEMANAGER/CREATED") + " " + file.CreationTime.ToShortDateString() + " " + file.CreationTime.ToShortTimeString() + " " + Environment.NewLine
                        + this.pluginLang.ReadField("/APPLANG/FILEMANAGER/MODIFIED") + " " + file.LastWriteTime.ToShortDateString() + " " + file.LastWriteTime.ToShortTimeString() + " ";
                    informationDialog(fileInformation);
                }
                else if (Directory.Exists(path))
                {
                    DirectoryInfo directory = new DirectoryInfo(path);
                }
            }
            catch (Exception ex)
            {
                informationDialog(ex.Message);
            }
        }

        private void pagingTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (pagingDirection == CFControls.CFListView.PagingDirection.DOWN)
                    this.listFiles.PageDown();
                else
                    this.listFiles.PageUp();

                updatePages();
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }
        }

        private string GetSelectedPath()
        {
            try
            {
                if (listFiles.SelectedItems.Count <= 0)
                    return "";

                int nSelected = listFiles.SelectedItems[0];
                if (nSelected < 0)
                    return "";

                return dtFiles.Rows[nSelected]["Path"].ToString();
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }

            return "";
        }

        private string GetSelectedDisplayName()
        {
            try
            {
                if (listFiles.SelectedItems.Count <= 0)
                    return "";

                int nSelected = listFiles.SelectedItems[0];
                if (nSelected < 0)
                    return "";

                return dtFiles.Rows[nSelected]["DisplayName"].ToString();
            }
            catch (Exception errmsg) { CFTools.writeError(errmsg.Message, errmsg.StackTrace); }

            return "";
        }
    }
}