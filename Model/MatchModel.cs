﻿using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;

namespace Twinder.Model
{
	public sealed class MatchModel
	{
		[JsonProperty("_id")]
		public string Id { get; set; }

		[JsonProperty("closed")]
		public bool Closed { get; set; }

		[JsonProperty("common_friend_count")]
		public int CommonFriendCount { get; set; }

		[JsonProperty("common_like_count")]
		public int CommonLikeCount { get; set; }

		[JsonProperty("created_date")]
		public DateTime CreatedDate { get; set; }

		[JsonProperty("dead")]
		public bool Dead { get; set; }

		[JsonProperty("last_activity_date")]
		public DateTime LastActivityDate { get; set; }

		[JsonProperty("message_count")]
		public int MessageCount { get; set; }

		[JsonProperty("messages")]
		public ObservableCollection<MessageModel> Messages { get; set; }

		[JsonProperty("muted")]
		public bool Muted { get; set; }

		[JsonProperty("participants")]
		public ObservableCollection<string> Participants { get; set; }

		[JsonProperty("pending")]
		public bool Pending { get; set; }

		[JsonProperty("is_super_like")]
		public bool IsSuperLike { get; set; }

		[JsonProperty("following")]
		public bool Following { get; set; }

		[JsonProperty("following_moments")]
		public bool FollowingMoments { get; set; }

		[JsonProperty("person")]
		public PersonModel Person { get; set; }


		public override string ToString()
		{
			return string.Format($"{Person.Name} {Person.Id}");
		}
	}
}
