﻿using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using GalaSoft.MvvmLight.Messaging;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Twinder.Helpers;
using Twinder.Model;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Windows.Threading;
using Twinder.Model.Authentication;
using System.Threading;
using System.IO;
using System.Windows.Data;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Twinder.ViewModel
{
	public class MainViewModel : ViewModelBase
	{
		private UpdatesModel _updates;
		public UpdatesModel Updates
		{
			get { return _updates; }
			set { Set(ref _updates, value); }
		}

		private ObservableCollection<MatchModel> _matchList;
		public ObservableCollection<MatchModel> MatchList
		{
			get { return _matchList; }
			set
			{
				Set(ref _matchList, value);

				MatchListCvs = new CollectionViewSource();
				MatchListCvs.Source = MatchList;
				MatchListCvs.Filter += FilterVM.MatchList_ApplyFilter;
				MatchListCvs.View.CollectionChanged += (object sender, NotifyCollectionChangedEventArgs e) =>
					FilterVM.FilteredMatchListCount = MatchListCvs.View.Cast<object>().Count();
			}
		}


		private CollectionViewSource _matchListCvs;
		public CollectionViewSource MatchListCvs
		{
			get { return _matchListCvs; }
			set { Set(ref _matchListCvs, value); }
		}


		private ObservableCollection<RecModel> _recList;
		public ObservableCollection<RecModel> RecList
		{
			get { return _recList; }
			set { Set(ref _recList, value); }
		}

		private UserModel _user;
		public UserModel User
		{
			get { return _user; }
			set { Set(ref _user, value); }
		}

		private AuthModel _auth;
		public AuthModel Auth
		{
			get { return _auth; }
			set { Set(ref _auth, value); }
		}

		/// <summary>
		/// Matches are added here when they receive an update or have new messages for serialization
		/// </summary>
		public ObservableCollection<MatchModel> UpdatedMatches { get; private set; }


		// Current connection status
		private string _connectionStatus = Properties.Resources.tinder_auth_connecting;
		public string ConnectionStatus
		{
			get { return _connectionStatus; }
			set
			{
				Set(ref _connectionStatus, value);
				if (value == Properties.Resources.tinder_auth_okay || value == Properties.Resources.tinder_auth_error)
					IsConnecting = false;
				else
					IsConnecting = true;
			}
		}

		// Whether to show progress bar or not based on current connection status
		private bool _isConnecting = true;
		public bool IsConnecting
		{
			get { return _isConnecting; }
			set { Set(ref _isConnecting, value); }
		}

		// Whether to show progress bar or not based on current connection status
		private bool _isConnected = true;
		public bool IsConnected
		{
			get { return _isConnected; }
			set { Set(ref _isConnected, value); }
		}

		// Holds reference to my view to subsribe to events
		public MainWindow MyView { get; set; }

		public AuthStatus AuthStatus { get; internal set; }
		public MatchesStatus MatchesStatus { get; internal set; }
		public RecsStatus RecsStatus { get; internal set; }

		public RelayCommand<MatchModel> OpenChatCommand { get; private set; }
		public RelayCommand<MatchModel> OpenMatchProfileCommand { get; private set; }
		public RelayCommand<MatchModel> UnmatchCommand { get; private set; }
		public RelayCommand<MatchModel> DownloadMatchDataCommand { get; private set; }
		
		public RelayCommand OpenRecsCommand { get; private set; }
		public RelayCommand OpenUserProfileCommand { get; private set; }
		public RelayCommand SetLocationCommand { get; private set; }
		public RelayCommand ExitCommand { get; private set; }
		public RelayCommand LoginCommand { get; private set; }
		public RelayCommand AboutCommand { get; private set; }
		public RelayCommand ForceDownloadMatchesCommand { get; private set; }
		public RelayCommand ForceDownloadMatchesFullCommand { get; private set; }
		public RelayCommand ForceDownloadRecsCommand { get; private set; }

		public MatchListFilterViewModel FilterVM { get; set; }

		public MainViewModel()
		{
			FilterVM = new MatchListFilterViewModel(this);

			UpdatedMatches = new ObservableCollection<MatchModel>();

			OpenChatCommand = new RelayCommand<MatchModel>((param) => OpenChat(param));
			OpenMatchProfileCommand = new RelayCommand<MatchModel>(param => OpenMatchProfile(param));
			UnmatchCommand = new RelayCommand<MatchModel>(param => Unmatch(param));
			DownloadMatchDataCommand = new RelayCommand<MatchModel>(param => DownloadFullMatchData(param));

			OpenRecsCommand = new RelayCommand(OpenRecs, () =>
			{
				return IsConnected && RecList != null && RecList.Count != 0;
			});

			OpenUserProfileCommand = new RelayCommand(OpenUserProfile, () => IsConnected);
			SetLocationCommand = new RelayCommand(SetLocation, () => IsConnected);

			ExitCommand = new RelayCommand(Exit);

			LoginCommand = new RelayCommand(Login);
			AboutCommand = new RelayCommand(About);

			ForceDownloadMatchesCommand = new RelayCommand(ForceDownloadMatches);
			ForceDownloadMatchesFullCommand = new RelayCommand(ForceDownloadMatchesFull);
			ForceDownloadRecsCommand = new RelayCommand(ForceDownloadRecs);

			Messenger.Default.Register<string>(this, MessengerToken.ForceUpdate, AddNewMatch);
			Messenger.Default.Register<string>(this, MessengerToken.GetMoreRecs, (input) => UpdateRecs(this, null));

			Application.Current.Exit += Current_Exit;



		}


		public async Task<bool> FullConnect()
		{
			ConnectionStatus = Properties.Resources.tinder_auth_connecting;

			// Starts authentication
			if (await Authenticate())
			{
				// Gets matches
				ConnectionStatus = Properties.Resources.tinder_update_getting_matches;
				await GetMatches();

				//ConnectionStatus = Properties.Resources.tinder_recs_getting_recs;
				// Gets recs
				await GetRecs();

				// Eveyrything done
				ConnectionStatus = Properties.Resources.tinder_auth_okay;
				return true;
			}
			else
			{
				ConnectionStatus = Properties.Resources.tinder_auth_error;
				return false;
			}
		}


		/// <summary>
		/// First thing called. Entry point of some sort. Calls other methods to get Tinder data
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public async void StartInitialize(object sender, EventArgs e)
		{
			ConnectionStatus = Properties.Resources.tinder_auth_connecting;

			// Only do full authorization if it's the first time or data is not saved yet
			if (Properties.Settings.Default.FirstStart || !Properties.Settings.Default.SerializationComplete)
			{
				if (await FullConnect())
				{ 
					int time = MatchList.Count * 3 / 60;

					// Dont ask just save nobody cares what user wants
					MessageBox.Show($"All your matches will be saved. It may take up to {time} minutes"
						+ "for the download to complete. I recommend not to cancel this action.",
						"Downloading data", MessageBoxButton.OK, MessageBoxImage.Information);

					Messenger.Default.Send(new SerializationPacket(MatchList, RecList, User),
							MessengerToken.ShowSerializationDialog);

					Properties.Settings.Default["FirstStart"] = false;
					Properties.Settings.Default.Save();

					StartUpdatingMatches();
					StartUpdatingRecs();
				}

			}
			// It's not the first time launching application
			else
			{
				// FIXME hangs application for a second
				// Deserializes matches and recs first
				MatchList = SerializationHelper.DeserializeMatchList();
				MatchListSetup();
				RecList = SerializationHelper.DeserializeRecList();


				if (await Authenticate())
				{
					// Updates last five matches
					Parallel.ForEach(MatchList.OrderByDescending(x => x.LastActivityDate).Take(3), async x =>
						{
							var updatedMatch = await TinderHelper.GetFullMatchData(x.Person.Id);
							SerializationHelper.UpdateMatchModel(x, updatedMatch);
							new Task(() => SerializationHelper.SerializeMatch(x)).Start();
						});

					FilterVM.SortMatchList();

					UpdateMatches(this, null);
					UpdateRecs(this, null);

					ConnectionStatus = Properties.Resources.tinder_auth_okay;

					// Starts automatic updates
					StartUpdatingMatches();
					StartUpdatingRecs();
				}
				else
					ConnectionStatus = Properties.Resources.tinder_auth_error;

			}
		}

		/// <summary>
		/// Tries authenticating with Tinder servers and getting User Data
		/// </summary>
		/// <returns></returns>
		public async Task<bool> Authenticate()
		{
			try
			{
				// We get tinder token everytime
				Auth = await TinderHelper.GetAuthData(Properties.Settings.Default.FbId, Properties.Settings.Default.FbToken);
				Properties.Settings.Default["TinderToken"] = Auth.Token;

				User = await TinderHelper.GetFullUserData();

				Properties.Settings.Default["LastUpdate"] = User.LatestUpdateDate;
				
				// Updates current location of user
				Properties.Settings.Default["Latitude"] = User.Pos.Latitude.Replace('.', ',');
				Properties.Settings.Default["Longtitude"] = User.Pos.Longtitude.Replace('.', ',');
				Properties.Settings.Default.Save();

				return true;
			}
			catch (TinderRequestException e)
			{
				MessageBox.Show(e.Message);
				return false;
			}
		}

		/// <summary>
		/// Tries connecting to Tinder servers and getting updates
		/// </summary>
		/// <returns></returns>
		public async Task<bool> GetMatches()
		{
			try
			{
				Updates = await TinderHelper.GetUpdates();

				if (MatchList == null)
					MatchList = Updates.Matches;

				MatchListSetup();

				Properties.Settings.Default["LastUpdate"] = Updates.LastActivityDate;
				Properties.Settings.Default.Save();
				return true;
			}
			catch (TinderRequestException e)
			{
				MessageBox.Show(e.Message);
				return false;
			}
		}
		
		/// <summary>
		/// Tries connecting to Tinder servers and getting recommendations
		/// </summary>
		/// <returns></returns>
		public async Task<bool> GetRecs()
		{
			try
			{
				var recs = await TinderHelper.GetRecommendations();
				if (recs.Recommendations != null)
				{
					// If it's the first time getting recs
					if (RecList == null)
						RecList = new ObservableCollection<RecModel>(recs.Recommendations);
					
					// Only useful if we force to download new recs in which case old
					// recs would be no use anyway
					RecList.Clear();
					foreach (var item in recs.Recommendations)
						RecList.Add(item);

					return true;
				}
				else
					return false;
			}
			catch (TinderRequestException e)
			{
				MessageBox.Show(e.Message);
				return false;
			}
		}

		private async void GetMoreRecs(string obj)
		{
			await GetRecs();
			Messenger.Default.Send(new SerializationPacket(RecList));
		}
		
		private void AddNewMatch(string message)
		{
			UpdateMatches(this, null);
		}

		private void StartUpdatingMatches()
		{
			DispatcherTimer timerMatches = new DispatcherTimer();
			timerMatches.Tick += UpdateMatches;
			timerMatches.Interval = new TimeSpan(0, 0, 10);
			timerMatches.Start();
		}

		private void StartUpdatingRecs()
		{
			DispatcherTimer timerRecs = new DispatcherTimer();
			timerRecs.Tick += UpdateRecs;
			timerRecs.Interval = new TimeSpan(0, 15, 0);
			timerRecs.Start();
		}

		private async void UpdateRecs(object sender, EventArgs e)
		{
			// There are no matches from before, try to get more
			if (RecList.Count == 0)
			{
				ConnectionStatus = Properties.Resources.tinder_recs_getting_recs;
				await GetRecs();
				ConnectionStatus = Properties.Resources.tinder_auth_okay;

				// If there are new matches, show dialog
				if (RecList.Count != 0)
					Messenger.Default.Send(new SerializationPacket(RecList),
							MessengerToken.ShowSerializationDialog);
			}
		}

		private async void UpdateMatches(object sender, EventArgs e)
		{
			try
			{
				var newUpdates = await TinderHelper.GetUpdates(Properties.Settings.Default.LastUpdate);

				if (newUpdates.Matches.Count != 0)
				{
					Properties.Settings.Default["LastUpdate"] = newUpdates.LastActivityDate;
					Properties.Settings.Default.Save();
					foreach (var newMatch in newUpdates.Matches)
					{
						var matchToUpdate = MatchList.Where(item => item.Id == newMatch.Id).FirstOrDefault();

						// There's an update to an existing match
						if (matchToUpdate != null)
						{
							// Adds new messages the to list
							foreach (var newMessage in newMatch.Messages)
							{
								if (!matchToUpdate.Messages.Contains(newMessage))
									matchToUpdate.Messages.Add(newMessage);
							}

							if (!UpdatedMatches.Contains(matchToUpdate))
								UpdatedMatches.Add(matchToUpdate);

							matchToUpdate.LastActivityDate = newMatch.LastActivityDate;

							new Task(() => SerializationHelper.SerializeMatch(matchToUpdate)).Start();


						}
						// There's a new match
						else
						{
							new Task(() => SerializationHelper.SerializeMatch(newMatch)).Start();
							MatchList.Insert(0, newMatch);
						}
					}
				}

				if (newUpdates.Blocks.Count != 0)
				{
					foreach (var unmatched in newUpdates.Blocks)
					{
						var match = MatchList.Where(x => x.Id == unmatched).FirstOrDefault();
						if (match != null)
						{
							SerializationHelper.MoveMatchToUnMatched(match);
							MatchList.Remove(match);
							MessageBox.Show(match.Person.Name + " unmatched you");
						}

					}

				}

				FilterVM.SortMatchList();
			}
			catch (TinderRequestException ex)
			{
				MessageBox.Show(ex.Message);
			}
		}

		/// <summary>
		/// Removes matches with null value (don't know why they are there) and 
		/// sorts by LastActivityDate in descending order
		/// </summary>
		private void MatchListSetup()
		{
			// Adds event handlers for each message list
			foreach (var item in MatchList)
			{
				item.Messages = item.Messages ?? new ObservableCollection<MessageModel>();

				item.Messages.CollectionChanged += Messages_CollectionChanged;
			}

			// Remove ghost matches, which I don't know why exist
			for (int i = 0; i < MatchList.Count; i++)
				if (MatchList[i].Person == null)
					MatchList.RemoveAt(i--);

			FilterVM.SortMatchList();
		}

		/// <summary>
		/// If there are new messages added to collection, updates the view
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			FilterVM.SortMatchList();
		}

		private async void DownloadFullMatchData(MatchModel param)
		{
			ConnectionStatus = Properties.Resources.tinder_update_getting_matches;

			var updatedMatch = await TinderHelper.GetFullMatchData(param.Person.Id);
			SerializationHelper.UpdateMatchModel(param, updatedMatch);
			new Task(() => SerializationHelper.SerializeMatch(param)).Start();

			ConnectionStatus = Properties.Resources.tinder_auth_okay;
			FilterVM.SortMatchList();
		}

		private void Unmatch(MatchModel match)
		{
			var decision = MessageBox.Show($"Do you really want to unmatch with {match.Person.Name}?",
				"Are you sure about that?", MessageBoxButton.YesNo, MessageBoxImage.Exclamation);

			if (decision == MessageBoxResult.Yes)
			{
				try
				{
					SerializationHelper.MoveMatchToUnMatchedByMe(match);
					TinderHelper.UnmatchPerson(match.Id);
					MatchList.Remove(match);
				}
				catch (TinderRequestException e)
				{
					MessageBox.Show(e.Message);
				}
			}
		}

		private void OpenUserProfile()
		{
			Messenger.Default.Send(User, MessengerToken.OpenMyProfile);
		}
		
		private void SetLocation()
		{
			Messenger.Default.Send("ayy", MessengerToken.ShowSetLocationWindow);
		}
		

		public void OpenRecs()
		{
			Messenger.Default.Send(RecList, MessengerToken.OpenRecommendations);
		}

		private void OpenChat(MatchModel match)
		{
			Messenger.Default.Send(match, MessengerToken.NewChatWindow);

			// Adding match to UpdateMatches list is the least complex way to force serialization
			// when user sends messages
			if (!UpdatedMatches.Contains(match))
				UpdatedMatches.Add(match);
		}

		/// <summary>
		/// Opens match profile and forces to download additional data
		/// </summary>
		/// <param name="match"></param>
		private void OpenMatchProfile(MatchModel match)
		{
			Messenger.Default.Send(match, MessengerToken.ShowMatchProfile);

			//DownloadFullMatchData(match);
		}

		/// <summary>
		/// Download all updates of matches
		/// </summary>
		private async void ForceDownloadMatches()
		{
			ConnectionStatus = Properties.Resources.tinder_update_getting_matches;
			try
			{
				bool setUpMatchList = false;
				var updates = await TinderHelper.GetUpdates();
			
				// FIXME For 200 matches this hangs for ~10s
				foreach (var item in updates.Matches.Where(x => x.Person != null))
				{
					var match = MatchList.FirstOrDefault(x => x.Person.Id == item.Person.Id);
					if (match != null)
						SerializationHelper.UpdateMatchModel(match, item);
					else
					{
						MatchList.Add(item);
						setUpMatchList = true;
					}
					//UpdatedMatches.Add(match);
				}

				if (setUpMatchList)
					MatchListSetup();
			
				Messenger.Default.Send(new SerializationPacket(MatchList), MessengerToken.ShowSerializationDialog);
			
				ConnectionStatus = Properties.Resources.tinder_auth_okay;

			}
			catch (TinderRequestException e)
			{
				MessageBox.Show(e.Message);
			}
		}

		/// <summary>
		/// Downloads full data of every match
		/// </summary>
		private void ForceDownloadMatchesFull()
		{
			Messenger.Default.Send(new SerializationPacket(MatchList, true), MessengerToken.ShowSerializationDialog);
		}

		/// <summary>
		/// Downloads all recommendations
		/// </summary>
		private void ForceDownloadRecs()
		{
			SerializationHelper.EmptyRecommendations();

			UpdateRecs(this, null);
		}

		private void Login()
		{
			Messenger.Default.Send("", MessengerToken.ShowLoginDialog);

		}

		private void About()
		{
			string appName = Properties.Resources.app_title;
			string version = "Version " + Assembly.GetEntryAssembly().GetName().Version.ToString(3);
			MessageBox.Show(version, appName);

		}

		/// <summary>
		/// Exits application
		/// </summary>
		private void Exit()
		{
			Application.Current.Shutdown();
		}

		/// <summary>
		/// Serializes match list on shutdown
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void Current_Exit(object sender, ExitEventArgs e)
		{
			 SerializationHelper.SerializeMatchList(UpdatedMatches, null);
		}

	}

	public class ConnectionStatusEventArgs : EventArgs
	{
		public ConnectionStatuss ConnectionStatus { get; set; }

		public ConnectionStatusEventArgs(ConnectionStatuss connectionStatus)
		{
			ConnectionStatus = connectionStatus;
		}
	}

	public enum ConnectionStatuss
	{
		Okay,
		Authenticating,
		GettingMatchesAndRecs,
		GettingMatches,
		GettingRecs,
		Error
	}

	public enum AuthStatus
	{
		Connecting,
		Okay,
		Error
	}

	public enum MatchesStatus
	{
		Waiting,
		Getting,
		Okay,
		Error
	}

	public enum RecsStatus
	{
		Waiting,
		Getting,
		Okay,
		Exhausted,
		Error
	}

	public enum DescriptionFilter
	{
		Both = 0,
		WithDescription,
		WithoutDescription
	}
}