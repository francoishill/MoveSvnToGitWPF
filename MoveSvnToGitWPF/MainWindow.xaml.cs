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

namespace MoveSvnToGitWPF
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, System.Windows.Forms.IWin32Window
	{
		List<MoveFromSvnToGit> moveItemList = null;

		public MainWindow()
		{
			InitializeComponent();
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
				moveItemList = MoveFromSvnToGit.GetListInRootSvnDir(
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

					listBox1.ItemsSource = moveItemList;
					progressBar1.Visibility = System.Windows.Visibility.Hidden;
				});
			},
			false,
			apartmentState: System.Threading.ApartmentState.STA);
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

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			this.WindowState = System.Windows.WindowState.Maximized;
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
