﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using Linxens.Core.Logger;
using Linxens.Core.Model;

namespace Linxens.Core.Service
{
    public class DataFileService
    {
        private readonly AppSettingsReader _config;
        public readonly ILogger _qadLogger;

        public readonly ILogger _technicalLogger;

        public DataFileService()
        {
            this._technicalLogger = TechnicalLogger.Instance;
            this._qadLogger = QadLogger.Instance;

            this._config = new AppSettingsReader();
            this.CurrentFile = new DataFile {Scrap = new List<Quality>()};
            this.FilesToProcess = new List<string>();

            try
            {
                this.InitConfig();
            }
            catch (Exception)
            {
                this._technicalLogger.LogError("Init Configuration", "Error on directory init please check your configuration file");
            }

            try
            {
                this.CheckDirectoryStructure(this.RootWorkingPath);
                this.LoadFileToProcess();
            }
            catch (Exception)
            {
                this._technicalLogger.LogError("Init Configuration", "Error on working directory structure init please check your configuration file");
            }
        }

        // Data for app.config
        public string UserApplication { get; set; }
        public string DomainWebService { get; set; }
        public string UserPwd { get; set; }
        public string MachineName { get; set; }
        public string UserEmail { get; set; }
        public string PrinterName { get; set; }
        public string WebServiceUrl { get; set; }
        public string WebServiceTimeOut { get; set; }

        public string RootDirPath { get; set; }

        // TODO => write in app.config on var update
        public string RootWorkingPath { get; set; }

        public DataFile CurrentFile { get; set; }

        public IList<string> FilesToProcess { get; set; }

        public void ReadFile(string path)
        {
            if (!File.Exists(path)) //throw new InvalidOperationException($"Path [{path}] not found");
            _technicalLogger.LogWarning("Read File", string.Format("Failed to read file. The file on path [{0}] is not exist", path));

            this.CurrentFile = new DataFile {Scrap = new List<Quality>()};

            string[] fileRawData = File.ReadAllLines(path);
            int currentLine = this.ReadFirstSection(fileRawData);
            currentLine = this.ReadScrapSection(fileRawData, currentLine);
            this.ReadLastSection(fileRawData, currentLine);
            _technicalLogger.LogInfo("Read File", string.Format("The file [{0}] is read successfully", path));
        }

        public void WriteFile()
        {
            List<string> tab = new List<string>();

            tab.Add("Repetitive:");
            tab.Add("Site:" + CurrentFile.Site);
            tab.Add("Emp:" + CurrentFile.Emp);
            tab.Add("Tr-Type:" + CurrentFile.TrType);
            tab.Add("Line:" + CurrentFile.Line);
            tab.Add("PN:" + CurrentFile.PN);
            tab.Add("OP:" + CurrentFile.OP);
            tab.Add("WC:" + CurrentFile.WC);
            tab.Add("MCH:" + CurrentFile.MCH);
            tab.Add("Lbl:" + CurrentFile.LBL);
            tab.Add("");
            tab.Add("Tape#:" + CurrentFile.TapeN);
            foreach (var quality in CurrentFile.Scrap)
            {
                tab.Add("Qty:" + quality.Qty + "" + "Rsn Code:" + quality.RsnCode);
            }
            tab.Add("");
            tab.Add("WR-PROD:");
            tab.Add("Tape#:" + CurrentFile.TapeN);
            tab.Add("Qty:" + CurrentFile.Qty);
            tab.Add("Defect:" + CurrentFile.Defect);
            tab.Add("Splices:" + CurrentFile.Splices);
            tab.Add("Dates:" + CurrentFile.DateTapes);
            tab.Add("Printers:" + CurrentFile.Printer);
            tab.Add("Number of conform parts:" + CurrentFile.NumbOfConfParts);

            File.AppendAllLines(Path.Combine(RootWorkingPath, WorkingType.RUNNING.ToString(), "runningFile.txt"), tab);
        }

        public void successFile()
        {
       
            File.Move(Path.Combine(RootWorkingPath, WorkingType.RUNNING.ToString(), "runningFile.txt"), Path.Combine(RootWorkingPath, WorkingType.DONE.ToString(), "runningFileDone.txt")); 
            File.Delete(WorkingType.TODO.ToString());
        }

        public void ErrorFile()
        {
            File.Move(Path.Combine(RootWorkingPath, WorkingType.RUNNING.ToString(), "runningFile.txt"), Path.Combine(RootWorkingPath, WorkingType.DONE.ToString(), "runningFileError.txt"));
        }
        private void LoadFileToProcess()
        {
            string todoDir = Path.Combine(this.RootWorkingPath, WorkingType.TODO.ToString());

            List<string> realFiles = Directory.GetFiles(this.RootDirPath).ToList();
            if (realFiles.Any())
                foreach (string realFile in realFiles)
                {
                    string fileName = Path.GetFileName(realFile).Replace(".txt", string.Format("_{0:yyyy-MM-dd  HH-mm-ss-fff}.txt", DateTime.Now));
                    _technicalLogger.LogInfo("Creation file to process", string.Format("The file [{0}] is created successfully", fileName));
                    if (fileName != null)
                    {
                        string destPath = Path.Combine(todoDir, fileName);
                        File.Copy(realFile, destPath, true);
                        _technicalLogger.LogInfo("Copy file on TODO directory", string.Format("The file [{0}] is copied on the TODO directory successfully", fileName));
                        File.Delete(realFile);
                        _technicalLogger.LogInfo("Delete File on the working directory", string.Format("The file [{0}] is deleted successfully on the working directory", fileName));
                    }
                }

            this.FilesToProcess = new DirectoryInfo(Path.Combine(this.RootWorkingPath, WorkingType.TODO.ToString())).GetFiles()
                        .OrderByDescending(f => f.LastWriteTime)
                        .Select(f => f.FullName)
                        .ToArray();
        }

        private void InitConfig()
        {
            this.RootDirPath = this._config.GetValue("RootDirectory", typeof(string)) as string;
            _technicalLogger.LogInfo("Init root directory", "The root directory is initialized successfully");
            this.RootWorkingPath = this._config.GetValue("RootWorkingDirectory", typeof(string)) as string;
            _technicalLogger.LogInfo("Init root working directory", "The root working directory is initialized successfully");
        }

        /// <summary>
        ///     Read the first data section
        /// </summary>
        /// <param name="txtFile"></param>
        /// <returns></returns>
        private int ReadFirstSection(string[] txtFile)
        {
            int i;
            for (i = 0; i < txtFile.Length; i++)
            {
                string line = txtFile[i];
                if (line == "") return i;

                string[] items = line.Split(':');
                switch (items[0])
                {
                    case "Site":
                        this.CurrentFile.Site = items[1];
                        break;
                    case "Emp":
                        this.CurrentFile.Emp = items[1];
                        break;
                    case "Tr-Type":
                        this.CurrentFile.TrType = items[1];
                        break;
                    case "Line":
                        this.CurrentFile.Line = items[1];
                        break;
                    case "PN":
                        this.CurrentFile.PN = items[1];
                        break;
                    case "OP":
                        this.CurrentFile.OP = Convert.ToInt32(items[1]);
                        break;
                    case "WC":
                        this.CurrentFile.WC = items[1];
                        break;
                    case "MCH":
                        this.CurrentFile.MCH = items[1];
                        break;
                    case "Lbl":
                        this.CurrentFile.LBL = items[1];
                        break;
                }
            }

            return i;
        }

        /// <summary>
        ///     read data for scrap section
        /// </summary>
        /// <param name="txtFile"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        private int ReadScrapSection(string[] txtFile, int startIndex)
        {
            string tape = "";
            int i;
            for (i = startIndex + 1; i < txtFile.Length; i++)
            {
                string line = txtFile[i];
                if (txtFile[i] == "") break;

                string[] items = line.Split(':');
                switch (items[0])
                {
                    case "Tape#":
                        tape = items[1];
                        break;
                    case "Qty":
                        this.CurrentFile.Scrap.Add(new Quality
                        {
                            Qty = items[1].Split(' ')[0],
                            RsnCode = items[2]
                        });
                        break;
                }
            }

            return i;
        }

        /// <summary>
        ///     read the last part of the file
        /// </summary>
        /// <param name="txtFile"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        private int ReadLastSection(string[] txtFile, int startIndex)
        {
            string qty ="";
            int i;
            for (i = startIndex + 1; i < txtFile.Length; i++)
            {
                string line = txtFile[i];
                if (txtFile[i] == "") break;
                string[] items = line.Split(':');
                switch (items[0])
                {
                    case "Qty":
                        this.CurrentFile.Qty = items[1];
                        qty = items[1];
                        break;
                    case "Defect":
                        this.CurrentFile.Defect = Convert.ToInt32(items[1]);
                        break;
                    case "Splices":
                        this.CurrentFile.Splices = Convert.ToInt32(items[1]);
                        break;
                    case "Dates":
                        this.CurrentFile.DateTapes = items[1];
                        break;
                    case "Printer":
                        this.CurrentFile.Printer = items[1];
                        break;
                    case "Number of conform parts":
                        this.CurrentFile.NumbOfConfParts = items[1];
                        break;
                    case "Tape#":
                        this.CurrentFile.TapeN = items[1];
                        break;
                }
            }

            float totalScrap = 0f;
            foreach (Quality quality in this.CurrentFile.Scrap)
            {
                bool isValid = true;
                float current;
                isValid = float.TryParse(quality.Qty, NumberStyles.Float, CultureInfo.InvariantCulture, out current);

                if (isValid)
                    totalScrap += current;
                

                //totalScrap += float.Parse(quality.Qty, CultureInfo.InvariantCulture);
            }
            float currentQty = float.Parse(qty, CultureInfo.InvariantCulture);
            float initialQty = currentQty - totalScrap;
            CurrentFile.InitialQty = initialQty.ToString(CultureInfo.InvariantCulture);
            return i;
        }
       
        /// <summary>
        ///     Check if directory structure exist
        /// </summary>
        /// <param name="path"></param>
        private void CheckDirectoryStructure(string path)
        {
            string todoDir = Path.Combine(path, WorkingType.TODO.ToString());
            string runningDir = Path.Combine(path, WorkingType.RUNNING.ToString());
            string doneDir = Path.Combine(path, WorkingType.DONE.ToString());
            string errorsDir = Path.Combine(path, WorkingType.ERROR.ToString());

            bool todoExist = Directory.Exists(todoDir);
            bool runningExist = Directory.Exists(runningDir);
            bool doneExist = Directory.Exists(doneDir);
            bool errorsExist = Directory.Exists(errorsDir);

            if (!todoExist) Directory.CreateDirectory(todoDir);
            _technicalLogger.LogInfo("Creation directory", "TODO directory is created successfully");
            if (!runningExist) Directory.CreateDirectory(runningDir);
            _technicalLogger.LogInfo("Creation directory", "RUNNING directory is created successfully");
            if (!doneExist) Directory.CreateDirectory(doneDir);
            _technicalLogger.LogInfo("Creation directory", "DONE directory is created successfully");
            if (!errorsExist) Directory.CreateDirectory(errorsDir);
            _technicalLogger.LogInfo("Creation directory", "ERRORS directory is created successfully");
        }

       //private string UpdateDataQlty(string txtbox, int resultat, )


        private enum WorkingType
        {
            TODO,
            RUNNING,
            DONE,
            ERROR
        }
    }
}