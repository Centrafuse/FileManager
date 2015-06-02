using System;
using System.Collections.Generic;
using System.Text;

namespace centrafuse.Plugins.FileManager
{
    class ExternalApplication
    {
        private string _appdisplayname;
        public string appdisplayname
        { get { return _appdisplayname; } set { _appdisplayname = value; } }

        private bool _apppausemusic;
        public bool apppausemusic
        { get { return _apppausemusic; } set { _apppausemusic = value; } }

        private bool _guiapplication;
        public bool guiapplication
        { get { return _guiapplication; } set { _guiapplication = value; } }

        private string _extensions;
        public string extensions
        { get { return _extensions; } set { _extensions = value; } }

        private Int32 _appinputdevice;
        public Int32 appinputdevice
        { get { return _appinputdevice; } set { _appinputdevice = value; } }

        private Int32 _appinputline;
        public Int32 appinputline
        { get { return _appinputline; } set { _appinputline = value; } }

        private int _display;
        public int display
        { get { return _display; } set { _display = value; } }

        private string _apppath;
        public string apppath
        { get { return _apppath; } set { _apppath = value; } }

        private string _appwindowname;
        public string appwindowname
        { get { return _appwindowname; } set { _appwindowname = value; } }

        private bool _appstartfullscreen;
        public bool appstartfullscreen
        { get { return _appstartfullscreen; } set { _appstartfullscreen = value; } }

        
    }
}
