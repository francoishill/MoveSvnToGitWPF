using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using SharedClasses;
using System.IO;
using System.Diagnostics;
using System.Windows.Shell;
using System.Collections.ObjectModel;

namespace MoveSvnToGitWPF
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, System.Windows.Forms.IWin32Window
	{
		ObservableCollection<MoveFromSvnToGit> moveItemList = new ObservableCollection<MoveFromSvnToGit>();

		public MainWindow()
		{
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			this.WindowState = System.Windows.WindowState.Maximized;
			listBox1.ItemsSource = moveItemList;
		}

		public IntPtr Handle
		{
			get { return this.GetHandle(); }
		}

		private void ActionFromSeparateThread(Action action)
		{
			this.Dispatcher.Invoke(action);
		}

		private void UpdateProgress(int progperc)
		{
			ActionFromSeparateThread(delegate
			{
				progressBar1.Value = progperc;
				progressBar1.UpdateLayout();
			});
		}

		private void AppendMessage(string mes)
		{
			ActionFromSeparateThread(delegate
			{
				textBox1.Text += mes + Environment.NewLine;
				textBox1.ScrollToEnd();
			});
		}

		private void listBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			listBox1.SelectedItem = null;
		}

		private void buttonAddAllInFolder_Click(object sender, RoutedEventArgs e)
		{
			ThreadingInterop.DoAction(delegate
			{
				string rootSvnCheckouts = null;//@"C:\Francois\Dev\VSprojects";
				string rootDirForGitClones = null;//@"C:\Francois\Other\tmp\testGit\Checkouts";
				string rootForRemoteGitRepos = null;//@"C:\Francois\Other\tmp\testGit\_repos";

				ActionFromSeparateThread(delegate
				{
					rootSvnCheckouts =
						FileSystemInterop.SelectFolder("Select root folder containing all SVN checkouts", @"C:\Francois\Dev\VSprojects", owner: this);
					if (rootSvnCheckouts == null) return;
					rootDirForGitClones =
						FileSystemInterop.SelectFolder("Select root folder for Git cloning", owner: this);
					if (rootDirForGitClones == null) return;
					rootForRemoteGitRepos =
						FileSystemInterop.SelectFolder("Select root folder for Git (remote) repos", owner: this);
					if (rootForRemoteGitRepos == null) return;//Wont actually do anything as it is the last line of this delegate function anyway
				});

				if (rootSvnCheckouts == null
					|| rootDirForGitClones == null
					|| rootForRemoteGitRepos == null)
					return;

				List<string> skippedDirectoriesDueToHttps;
				var tmpListInFolder = MoveFromSvnToGit.GetListInRootSvnDir(
					rootSvnCheckouts,
					rootDirForGitClones,
					rootForRemoteGitRepos,
					true, out skippedDirectoriesDueToHttps,
					UpdateProgress);

				ActionFromSeparateThread(delegate
				{
					if (skippedDirectoriesDueToHttps.Count > 0)
						UserMessages.ShowWarningMessage(
							this,
							"The following directories are skipped because their SVN urls are https (not currently supported):"
							+ Environment.NewLine + Environment.NewLine
							+ string.Join(Environment.NewLine, skippedDirectoriesDueToHttps));

					progressBar1.Visibility = System.Windows.Visibility.Hidden;

					foreach (var item in tmpListInFolder)
						moveItemList.Add(item);
				});
			},
			false,
			apartmentState: System.Threading.ApartmentState.STA);
		}

		private string lastUsedSvnDir = null;
		private string lastUsedLocalGitCloneDir = null;
		private string lastUsedLocalGitRepoDir = null;
		private void buttonSingleFolder_Click(object sender, RoutedEventArgs e)
		{
			var svnDirectory = FileSystemInterop.SelectFolder("Please select the local svn directory", lastUsedSvnDir ?? @"C:\");
			if (svnDirectory == null) return;
			lastUsedSvnDir = svnDirectory;

			var localGitCloneDir = FileSystemInterop.SelectFolder("Now select the local Git directory in which to clone the working copy", lastUsedLocalGitCloneDir ?? svnDirectory);
			if (localGitCloneDir == null) return;
			lastUsedLocalGitCloneDir = localGitCloneDir;

			var localGitRepoDir = FileSystemInterop.SelectFolder("Finally select the directory for the Git remote repository", lastUsedLocalGitRepoDir ?? localGitCloneDir);
			if (localGitRepoDir == null) return;
			lastUsedLocalGitRepoDir = localGitRepoDir;

			progressBar1.IsIndeterminate = true;
			try
			{
				bool skippedDueToHttps;
				MoveFromSvnToGit tmpNewItem = MoveFromSvnToGit.GetMoveItemFromFolder(svnDirectory, localGitCloneDir, localGitRepoDir, out skippedDueToHttps);
				if (skippedDueToHttps)
				{
					UserMessages.ShowErrorMessage("The directory cannot be merged because the svn repo is on https, this is not currently supported. Consider doing a SVN dump from the SVN repo");
					return;
				}
				else if (tmpNewItem == null)
				{
					UserMessages.ShowErrorMessage("Something went wrong, we cannot convert the SVN repo to a Git repo");
					return;
				}

				moveItemList.Add(tmpNewItem);
			}
			finally
			{
				progressBar1.IsIndeterminate = false;
			}
		}

		private void buttonRunAll_Click(object sender, RoutedEventArgs e)
		{
			ThreadingInterop.DoAction(delegate
			{
				SetProgressState(TaskbarItemProgressState.Normal);
				SetProgressValue(0);

				var distinct_GitCloned =
					moveItemList.Select(mi => Path.GetDirectoryName(mi.LocalGitClonedFolder))
					.Distinct()
					.ToList();
				var distinct_RemoteGitRepo =
					moveItemList.Select(mi => Path.GetDirectoryName(mi.RemoteGitRepo))
					.Distinct()
					.ToList();

				int doneCount = 0;
				foreach (var moveitem in moveItemList)
				{
					moveitem.MoveNow(
						AppendMessage,
						true,
						false,
						false);
					SetProgressValue((int)Math.Truncate(100D * (double)++doneCount / (double)moveItemList.Count));
				}

				//Open the folders in explorer (using the root of each moveitem)
				distinct_GitCloned.ToList().ForEach(fol => Process.Start("explorer", fol));
				distinct_RemoteGitRepo.ToList().ForEach(fol => Process.Start("explorer", fol));

				SetProgressState(TaskbarItemProgressState.None);
			},
			false);
		}

		private void SetProgressState(System.Windows.Shell.TaskbarItemProgressState state)
		{
			ActionFromSeparateThread(delegate { this.TaskbarItemInfo.ProgressState = state; });
		}

		private void SetProgressValue(int valuePercentage)
		{
			ActionFromSeparateThread(delegate { this.TaskbarItemInfo.ProgressValue = ((double)valuePercentage) / 100D; });
		}

		private void labelAbout_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			AboutWindow2.ShowAboutWindow(new System.Collections.ObjectModel.ObservableCollection<DisplayItem>()
			{
				new DisplayItem("Author", "Francois Hill"),
				new DisplayItem("Icon(s) obtained from", "http://www.iconfinder.com", "http://www.iconfinder.com/icondetails/66891/128/move_tag_icon")

			});
		}
	}

	public class NullableIntToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			bool hideInsteadOfCollapse = parameter != null && parameter.ToString().Equals("HideInsteadOfCollapse", StringComparison.InvariantCultureIgnoreCase);
			if ((value is int) && (int)value == 0)
				return hideInsteadOfCollapse ? Visibility.Hidden : Visibility.Collapsed;
			else
				return Visibility.Visible;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
