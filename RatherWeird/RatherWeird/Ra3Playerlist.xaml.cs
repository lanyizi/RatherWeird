﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using Newtonsoft.Json;

namespace RatherWeird
{
    
    /// <summary>
    /// Interaktionslogik für Ra3Playerlist.xaml
    /// </summary>
    public partial class Ra3Playerlist : UserControl
    {
        private CancellationTokenSource _tokenSource;
        private readonly ObservableCollection<User> _ra3Users = new ObservableCollection<User>();
        public Ra3Playerlist()
        {
            InitializeComponent();

            lstView.ItemsSource = _ra3Users;
        }

        public void LaunchReoccuringTask()
        {
            _tokenSource = new CancellationTokenSource();

            async void Repeated(Task _)
            {
                CncGeneralInfo info = await GatherData();
                InsertData(info);
                await Task.Delay(1000 * 60, _tokenSource.Token).ContinueWith(_2 => Repeated(_2), _tokenSource.Token);
            }

            Task.Delay(1000, _tokenSource.Token).ContinueWith((Action<Task>) Repeated, _tokenSource.Token);
        }

        public void StopReoccuringTask()
        {
            _tokenSource.Cancel();
        }

        private async Task<CncGeneralInfo> GatherData()
        {
            CncGeneralInfo info;
            WebRequest req = WebRequest.CreateHttp(Constants.CncOnlinePlayerInfo);

            var res = await req.GetResponseAsync();
            HttpWebResponse response = (HttpWebResponse) res;

            using (Stream streamResponse = response.GetResponseStream())
            using (StreamReader sr = new StreamReader(streamResponse))
            {
                string body = sr.ReadToEnd();
                info = JsonConvert.DeserializeObject<CncGeneralInfo>(body);
            }
            
            return info;

        }

        private void InsertData(CncGeneralInfo info)
        {
            lstView.Dispatcher.Invoke(() =>
            {
                foreach (var ra3User in info.Ra3.Users)
                {
                    if (!_ra3Users.Contains(ra3User.Value))
                    {
                        _ra3Users.Add(ra3User.Value);
                    }
                }


                for (int i = _ra3Users.Count - 1; i <= 0; i--)
                {
                    if (!info.Ra3.Users.Values.Contains(_ra3Users[i]))
                    {
                        _ra3Users.RemoveAt(i);
                    }
                }
            });
        }
        
    }
    
    [DataContract]
    internal class CncGeneralInfo
    {
        [DataMember(Name = "bfme")]
        public Game Bfme { get; set; }
        [DataMember(Name = "bfme2")]
        public Game Bfme2 { get; set; }
        [DataMember(Name = "cnc3")]
        public Game Cnc3 { get; set; }
        [DataMember(Name = "cnc3kw")]
        public Game Cnc3Kw { get; set; }
        [DataMember(Name = "generals")]
        public Game Generals { get; set; }
        [DataMember(Name = "generalzh")]
        public Game Generalszh { get; set; }
        [DataMember(Name = "ra3")]
        public Game Ra3 { get; set; }
        [DataMember(Name = "rotwk")]
        public Game Rotwk { get; set; }
    }

    [DataContract]
    internal class Game
    {
        [DataMember(Name = "lobbies")]
        public Lobby Lobbies { get; set; }
        [DataMember(Name = "users")]
        public IDictionary<string, User> Users { get; set; }
        [DataMember(Name = "games")]
        public MetaMatch Games { get; set; }
    }
    [DataContract]
    internal class Lobby
    {
        [DataMember(Name = "chat")]
        public int Chat { get; set; }
        [DataMember(Name = "hosting")]
        public int Hosting { get; set; }
        [DataMember(Name = "playing")]
        public int Playing { get; set; }
    }

    [DataContract]
    internal class User
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }
        [DataMember(Name = "pid")]
        public int Pid { get; set; }
        
        [DataMember(Name = "nickname")]
        public string Nickname { get; set; }

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ Nickname.GetHashCode() ^ Pid.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as User;
            if (other == null)
            {
                return false;
            }

            if (obj == this)
            {
                return true;
            }

            if (!other.Id.Equals(Id))
            {
                return false;
            }

            if (!other.Pid.Equals(Pid))
            {
                return false;
            }

            if (!other.Nickname.Equals(Nickname))
            {
                return false;
            }

            return true;
        }
    }

    [DataContract]
    internal class MetaMatch
    {
        [DataMember(Name = "playing")]
        public Match[] Playing { get; set; }
        [DataMember(Name = "staging")]
        public Match[] Staging { get; set; }
    }

    [DataContract]
    internal class Match
    {
        [DataMember(Name = "cmdCRC")]
        public string CmdCRC { get; set; }
        [DataMember(Name = "exeCRC")]
        public string ExeCRC { get; set; }
        [DataMember(Name = "gamever")]
        public string Gamever { get; set; }
        [DataMember(Name = "host")]
        public User Host { get; set; }
        [DataMember(Name = "iniCRC")]
        public string IniCRC { get; set; }
        [DataMember(Name = "map")]
        public string Map { get; set; }
        [DataMember(Name = "maxRealPlayers")]
        public string MaxRealPlayers { get; set; }
        [DataMember(Name = "maxplayers")]
        public string Maxplayers { get; set; }
        [DataMember(Name = "numObservers")]
        public string NumObservers { get; set; }
        [DataMember(Name = "numRealPlayers")]
        public string NumRealPlayers { get; set; }
        [DataMember(Name = "numplayers")]
        public string Numplayers { get; set; }
        [DataMember(Name = "obs")]
        public string Obs { get; set; }
        [DataMember(Name = "pings")]
        public string Pings { get; set; }
        [DataMember(Name = "pw")]
        public string Pw { get; set; }
        [DataMember(Name = "title")]
        public string Title { get; set; }
        [DataMember(Name = "version")]
        public string Version { get; set; }
        [DataMember(Name = "players")]
        public User[] Players { get; set; }
    }
}
