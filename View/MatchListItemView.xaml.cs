﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Twinder.Model;

namespace Twinder.View
{
	/// <summary>
	/// Interaction logic for MatchListItemView.xaml
	/// </summary>
	public partial class MatchListItemView : UserControl
	{
		public MatchListItemView()
		{
			InitializeComponent();

		}

		private void CreateMatchProfileView(MatchModel match)
		{
			MatchProfileView window = new MatchProfileView(match);
			window.Show();
		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			Binding myBinding = new Binding();

			myBinding.RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListView), 1);
		}
	}
}
