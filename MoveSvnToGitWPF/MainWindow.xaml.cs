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

namespace MoveSvnToGitWPF
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		List<MoveFromSvnToGit> moveItemList = null;

		public MainWindow()
		{
			InitializeComponent();
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
				string rootSvnCheckouts =
					@"C:\Francois\Dev\VSprojects";
					//FileSystemInterop.SelectFolder("Select root folder containing all SVN checkouts", @"C:\Francois\Dev\VSprojects");
				if (rootSvnCheckouts == null) return;
				string rootDirForGitClones =
					@"C:\Francois\Other\tmp\testGit\Checkouts";
					//FileSystemInterop.SelectFolder("Select root folder for Git cloning");
				if (rootDirForGitClones == null) return;
				string rootForRemoteGitRepos =
					@"C:\Francois\Other\tmp\testGit\_repos";
					//FileSystemInterop.SelectFolder("Select root folder for Git (remote) repos");
				if (rootForRemoteGitRepos == null) return;
				List<string> skippedDirectoriesDueToHttps;
				moveItemList = MoveFromSvnToGit.GetListInRootSvnDir(
					rootSvnCheckouts,
					rootDirForGitClones,
					rootForRemoteGitRepos,
					true, out skippedDirectoriesDueToHttps,
					UpdateProgress);
				ActionFromSeparateThread(delegate
				{
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
				var distinct_GitCloned =
					moveItemList.Select(mi => Path.GetDirectoryName(mi.LocalGitClonedFolder))
					.Distinct()
					.ToList();
				var distinct_RemoteGitRepo =
					moveItemList.Select(mi => Path.GetDirectoryName(mi.RemoteGitRepo))
					.Distinct()
					.ToList();

				foreach (var moveitem in moveItemList)
					moveitem.MoveNow(
						AppendMessage,
						true,
						false,
						false);

				//Open the folders in explorer (using the root of each moveitem)
				distinct_GitCloned.ToList().ForEach(fol => Process.Start("explorer", fol));
				distinct_RemoteGitRepo.ToList().ForEach(fol => Process.Start("explorer", fol));
			},
			false);
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
