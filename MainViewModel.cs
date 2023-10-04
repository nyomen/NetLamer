using CommunityToolkit.Mvvm.Input;
using NetLamer.Helpers;
using NetLamer.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace NetLamer
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private const string SlowString = "Slow";
        private const string MediumString = "Medium";
        private const string FastString = "Fast";
        private const string UltraFastString = "UltraFast";

        private bool setByProgramGlobal = false;

        public ICommand RefreshListCommand { get; }

        private bool isBusy = false;
        public bool IsBusy
        {
            get { return isBusy; }
            set
            {
                if (value.Equals(isBusy)) return;
                isBusy = value;
                OnPropertyChanged(nameof(IsBusy));
            }
        }

        private Speed selectedSpeed = new Speed(SlowString, "500KB");
        public Speed SelectedSpeed
        {
            get { return selectedSpeed; }
            set
            {
                if (value.Equals(selectedSpeed)) return;
                selectedSpeed = value;
                if (!setByProgramGlobal) activateOrDeactivatePolicy(limiterIsActive, selectedProcess?.Process);
                OnPropertyChanged(nameof(SelectedSpeed));
            }
        }

        private List<Speed> speedChoices = new List<Speed>();
        public List<Speed> SpeedChoices => speedChoices;

        private bool limiterIsActive = false;
        public bool LimiterIsActive
        {
            get
            {
                return limiterIsActive;
            }
            set
            {
                if (value.Equals(limiterIsActive)) return;
                limiterIsActive = value;
                if (!setByProgramGlobal) activateOrDeactivatePolicy(limiterIsActive, selectedProcess?.Process);
                OnPropertyChanged(nameof(LimiterIsActive));
            }
        }

        private ProcessWithIcon? selectedProcess;
        public ProcessWithIcon? SelectedProcess
        {
            get
            {
                return selectedProcess;
            }
            set
            {
                selectedProcess = value;
                setByProgramGlobal = true;
                if (selectedProcess is not null) updatePolicyValues(selectedProcess.Process, setByProgramGlobal);
                setByProgramGlobal = false;
                OnPropertyChanged(nameof(SelectedProcess));
            }
        }

        private ObservableCollection<ProcessWithIcon> processListWithIcon = new ObservableCollection<ProcessWithIcon>();
        public ObservableCollection<ProcessWithIcon> ProcessListWithIcons
        {
            get
            {
                return processListWithIcon;
            }
            set
            {
                if (value is null) return;
                processListWithIcon = value;
                OnPropertyChanged(nameof(ProcessListWithIcons));
            }
        }

        public MainViewModel()
        {
            RefreshListCommand = new RelayCommand(refreshProcessListFromCommand);
            refreshProcessListFromCommand();
            generateSpeeds();
        }

        private void generateSpeeds()
        {
            speedChoices.Add(selectedSpeed);
            speedChoices.Add(new Speed(MediumString, "1MB"));
            speedChoices.Add(new Speed(FastString, "2MB"));
            speedChoices.Add(new Speed(UltraFastString, "3MB"));
        }

        private void refreshProcessList()
        {
            List<string> existingPolicyExecuteableNames = getExistingPolicyExecutableNames();
            var processListWithIconAddTemp = new ObservableCollection<ProcessWithIcon>();
            var processListWithIconRemoveTemp = new ObservableCollection<ProcessWithIcon>();

            Process[] processArray = Process.GetProcesses();
            var processList =
                processArray
                .ToList<Process>()
                .GroupBy(p => p.ProcessName)
                .Select(g => g.First())
                .OrderBy(o => o.ProcessName)
                .ToList();

            foreach (var process in processList)
            {
                var processFileName = string.Empty;
                try
                {
                    processFileName = process.MainModule?.FileName;
                }
                catch (Exception) { continue; }

                var processModuleName = Path.GetFileName(processFileName);


                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var found = processListWithIcon.FirstOrDefault(x => x.Process?.ProcessName == process.ProcessName);
                    if (found is null)
                    {
                        var newProcess = new ProcessWithIcon(
                            process,
                            getImageFromExe(processFileName),
                            processModuleName is not null ? existingPolicyExecuteableNames.Contains(processModuleName) : false
                            );
                        processListWithIconAddTemp.Add(newProcess);
                    }
                    else
                    {
                        found.UpdateProcess(process, getImageFromExe(processFileName), processModuleName is not null ? existingPolicyExecuteableNames.Contains(processModuleName) : false);
                    }
                }));
            }



            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    foreach (var process in ProcessListWithIcons)
                    {
                        if (process.Process is not null && !processList.Contains(process.Process))
                            processListWithIconRemoveTemp.Add(process);
                    }
                    foreach (var process in processListWithIconRemoveTemp)
                    {
                        ProcessListWithIcons.Remove(process);
                    }
                    ProcessListWithIcons = new ObservableCollection<ProcessWithIcon>(ProcessListWithIcons.Concat(processListWithIconAddTemp).OrderBy(o => o.Process?.ProcessName));
                }));
        }

        private void refreshProcessListFromCommand()
        {
            IsBusy = true;

            Task<Boolean>.Factory.StartNew(() =>
            {
                refreshProcessList();
                return true;
            }).ContinueWith(t =>
            {
                if (t.Result)
                    IsBusy = false;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private List<string> getExistingPolicyExecutableNames()
        {
            List<string> existingPolicyExecutableNames = new();

            string scriptFindPolicyText = new StringBuilder()
                .Append("powershell Get-NetQoSPolicy")
                .ToString();
            string output = PowerShellHandler.Command(scriptFindPolicyText);
            if (output.Trim() != string.Empty)
            {
                Regex pattern = new Regex(@"AppPathName    : (.*?)\r");
                existingPolicyExecutableNames = pattern
                            .Matches(output)
                            .Cast<Match>()
                            .Select(x => x.Groups[1].Value)
                            .ToList();
            }
            return existingPolicyExecutableNames;
        }

        private void activateOrDeactivatePolicy(bool limiterIsActive, Process? process)
        {
            if (process is null) return;
            IsBusy = true;
            Task<Boolean>.Factory.StartNew(() =>
            {
                if (limiterIsActive)
                {
                    removePolicy(process.ProcessName);
                    addPolicy(process.ProcessName, process.MainModule?.ModuleName, selectedSpeed.SpeedForQoS);
                }
                else
                {
                    removePolicy(process.ProcessName);
                    Thread.Sleep(1000);
                }
                refreshProcessList();
                return true;
            }).ContinueWith(t =>
            {
                if (t.Result) IsBusy = false;
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void addPolicy(string processName, string? moduleName, string somethingPerSecond)
        {
            string scriptAddText = new StringBuilder()
                    .Append("powershell New-NetQoSPolicy -Name NetLamer")
                    .Append(processName)
                    .Append(" -AppPathNameMatchCondition ")
                    .Append(moduleName)
                    .Append(" -ThrottleRateActionBitsPerSecond ")
                    .Append(somethingPerSecond)
                    .ToString();
            string output = PowerShellHandler.Command(scriptAddText);
        }

        private void removePolicy(string processName)
        {
            string scriptDeleteText = new StringBuilder()
                   .Append(@"Start-Process powershell.exe -WindowStyle Hidden -Verb RunAs -ArgumentList 'Remove-NetQoSPolicy -Name NetLamer")
                   .Append(processName)
                   .Append(@" -Confirm:$false'")
                   .ToString();
            string output = PowerShellHandler.Command(scriptDeleteText);
        }

        private void updatePolicyValues(Process? process, bool setByProgram = false)
        {
            IsBusy = true;
            string speedString = string.Empty;
            Task<Boolean>.Factory.StartNew(() =>
            {
                string scriptFindPolicyText = new StringBuilder()
                    .Append("powershell Get-NetQoSPolicy -Name NetLamer")
                    .Append(process?.ProcessName)
                    .ToString();
                string output = PowerShellHandler.Command(scriptFindPolicyText);
                if (output.Trim() != string.Empty)
                {
                    int mb = 0;
                    bool result = int.TryParse(output.Split("ThrottleRate   :")[1]?.Trim()?.Split(" ")[0]?.Replace(".", string.Empty), out mb);
                    if (result)
                    {
                        switch (mb)
                        {
                            case 512:
                                speedString = SlowString;
                                break;
                            case 1049:
                                speedString = MediumString;
                                break;
                            case 2097:
                                speedString = MediumString;
                                break;
                            case 3146:
                                speedString = FastString;
                                break;
                            default:
                                speedString = string.Empty;
                                break;
                        };
                    }
                }
                return true;
            }).ContinueWith(t =>
            {
                if (t.Result)
                {
                    setByProgramGlobal = setByProgram;
                    if (speedString != string.Empty)
                        setPolicyValuesForProcess(true, speedString);
                    else
                        setPolicyDefaultValuesForProcess();
                    IsBusy = false;
                    setByProgramGlobal = false;
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void setPolicyValuesForProcess(bool activ, string speedName)
        {
            var speedChoice = SpeedChoices.FirstOrDefault(_ => _.Name == speedName);
            LimiterIsActive = activ;
            SelectedSpeed = speedChoice is null ? SpeedChoices.First() : speedChoice;
        }

        private void setPolicyDefaultValuesForProcess()
        {
            setPolicyValuesForProcess(false, SlowString);
        }

        private BitmapSource? getImageFromExe(string? filePath)
        {
            if (filePath is null || filePath.Trim() == string.Empty) return null;
            var sysicon = System.Drawing.Icon.ExtractAssociatedIcon(filePath as string);
            if (sysicon is null) return null;
            var bmpSrc = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        sysicon.Handle,
                        System.Windows.Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            sysicon.Dispose();
            return bmpSrc;
        }

        #region INotifyPropertyChanged Members  

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }
}
