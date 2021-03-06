using Discord;
using Discord.Commands;
using System.IO;
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Configuration;
using Cleverbot.Net;
using System.Security.Cryptography;//for the killswitch
using System.Linq;//for the killswitch

class Program
{
	static void Main(string[] args) => new Program().Start();

	private DiscordClient _client;

	public void Start()
	{
		_client = new DiscordClient();

		_client.Log.Message += (s, e) => Console.WriteLine($"[{e.Severity} - {DateTime.UtcNow.Hour}:{DateTime.UtcNow.Minute}:{DateTime.UtcNow.Second}] {e.Source}: {e.Message}");

		CleverbotSession session = null;

		_client.UsingCommands(x =>
		{
			x.PrefixChar = '+';
			x.AllowMentionPrefix = true;
			x.HelpMode = HelpMode.Public;
		});

		char p = '+';
		// var sepchar = Path.DirectorySeparatorChar;
		string topic = "Ready to play a Mini TWOW!";

		_client.GetService<CommandService>().CreateCommand("botok") //create command
			   .Alias("ping", "status") // add some aliases
			   .Description("Checks if the bot is online.") //add description, it will be shown when +help is used
			   .Do(async e =>
			   {
				   if (IsMTWOWChannel(e.Server.Id, e.Channel.Id))
					   await e.Channel.SendMessage($"bot is online \ud83d\udc4c");
			   });

        _client.GetService<CommandService>().CreateCommand("kill") //killswitch
               .Alias("shutdown") 
               .Description("Shuts down the bot if correct parameter is given") 
               .Parameter("keyword", ParameterType.Multiple)
               .Do(e => {
                   string keyhash = "0a7a27bedd1f822ac176d55217c0cdc9e8573f173d2b1a525e3607a7614a29b7";
                   try
                   {
                       if (e.Channel.IsPrivate && sha256_hash(e.GetArg("keyword")) == keyhash)
                       {
                           System.Environment.Exit(1);
                       }
                   }
                   catch { }
               });

		_client.GetService<CommandService>().CreateCommand("prepare")
			   .Alias("setup")
			   .Description("Prepares the current channel for a Mini TWOW.")
			   .Do(async e =>
			   {
				   User bot = e.Server.GetUser(_client.CurrentUser.Id);
				   if (bot.GetPermissions(e.Channel).ManageChannel)
				   {
					   if (e.User.GetPermissions(e.Channel).ManageChannel)
					   {
						   Clear("data", e.Server.Id);
						   SaveLine("data", e.Channel.Id.ToString(), e.Server.Id, 1);
						   SaveLine("data", "0", e.Server.Id, 2);
						   SaveLine("data", "null", e.Server.Id, 3);
						   Clear("users", e.Server.Id);
						   //see wiki for dev notes/file layout
						   await e.Channel.Edit(e.Channel.Name, $"{topic}\n{e.Channel.Topic.Replace($"{topic}", "")}", e.Channel.Position);
						   await e.Channel.SendMessage($"This channel is now ready to play Mini TWOWs!");
					   }
					   else
					   {
						   await e.Channel.SendMessage($"You must have the `MANAGE_CHANNELS` permission to use this command!");
					   }
				   }
				   else
				   {
					   await e.Channel.SendMessage($"Please give the bot permissions to manage the channel!");
				   }
			   });

		_client.GetService<CommandService>().CreateCommand("create")
			   .Alias("make")
			   .Description("Creates a Mini TWOW game.")
			   .Do(async e =>
			   {
				   ulong mtChannel = 000000000000000000;
				   string data = LoadLine("data", e.Server.Id, 1);
				   bool parseResult = ulong.TryParse(data, out mtChannel);
				   if (parseResult)
				   {
					   if (e.Channel.Id == mtChannel && e.Server.GetUser(_client.CurrentUser.Id).GetPermissions(e.Channel).SendMessages)
					   {
						   int gamestatus = 100;
						   data = LoadLine("data", e.Server.Id, 2);
						   bool newParseResult = int.TryParse(data, out gamestatus);
						   if (newParseResult)
						   {
							   if (gamestatus == 0)
							   {
								   SaveLine("data", "1", e.Server.Id, 2);
								   SaveLine("data", e.User.Id.ToString(), e.Server.Id, 3);
								   Clear("users", e.Server.Id);
								   await e.Channel.SendMessage($"You have successfully created a Mini TWOW game! Run `{p}join` to join the game, and run `{p}start` when you're ready to start the game.");
							   }
							   else
							   {
								   await e.Channel.SendMessage($"A game is already running. Please wait for it to finish!");
							   }
						   }
						   else
						   {
							   await e.Channel.SendMessage($"The data file has been corrupted. Please ask a user with `MANAGE_CHANNELS` perms to do `{p}prepare`.");
						   }
					   }
					   else
					   {
						   // await e.Channel.SendMessage($"Please go to <#{mtChannel}> to start a Mini TWOW!");
					   }
				   }
				   else
				   {
					   await e.Channel.SendMessage($"Please get a user with `MANAGE_CHANNELS` permissions to run the `{p}prepare` command before starting a Mini TWOW.");
				   }
			   });

		_client.GetService<CommandService>().CreateCommand("join")
			   .Description("Joins an active Mini TWOW game.")
			   .Do(async e =>
			   {
				   try
				   {
					   if (IsMTWOWChannel(e.Server.Id, e.Channel.Id))
					   {
						   int gamestatus = 100;
						   string data = LoadLine("data", e.Server.Id, 2);
						   bool parseResult = int.TryParse(data, out gamestatus);
						   if (parseResult)
						   {
							   if (gamestatus == 1)
							   {
								   string[] users = LoadFile("users", e.Server.Id);
								   if (users == null)
								   {
									   string newUsers = $"{e.User.Id.ToString()}\n";
									   SaveFile("users", newUsers, e.Server.Id);
									   await e.Channel.SendMessage($"You have successfully joined the game.");
								   }
								   bool inGame = false;
								   foreach (string user in users)
								   {
									   ulong userID = 000000000000000000;
									   bool parse = ulong.TryParse(user, out userID);
									   if (parse)
										   inGame |= userID == e.User.Id;
								   }
								   if (inGame)
									   await e.Channel.SendMessage($"You are already in the game!");
								   else
								   {
									   string newUsers = users.ToString();
									   newUsers += $"{e.User.Id.ToString()}\n";
									   SaveFile("users", newUsers, e.Server.Id);
									   await e.Channel.SendMessage($"You have successfully joined the game.");
								   }
							   }
							   else if (gamestatus == 0)
							   {
								   await e.Channel.SendMessage($"No game is currently running. Type `{p}create` to create a game!");
							   }
							   else
							   {
								   await e.Channel.SendMessage($"It is too late to join the game!");
							   }
						   }
						   else
						   {
							   await e.Channel.SendMessage($"The data file has been corrupted. Please ask a user with `MANAGE_CHANNELS` permissions to do `{p}prepare`.");
						   }
					   }
				   }
			catch (Exception error) { Console.WriteLine($"[ERROR] somethin borked during {p}join: {error.ToString()}"); }
			   });

		_client.GetService<CommandService>().CreateCommand("chat")
			   .Description("Talk with the bot")
			   .Parameter("sentence", ParameterType.Unparsed)
			   .Do(async e =>
			   {
				   try
				   {
					   if (session == null)
					   {
						   string ChatUser = File.ReadAllLines("logins.txt")[1];
						   string ChatKey = File.ReadAllLines("logins.txt")[2];
						   session = await CleverbotSession.NewSessionAsync(ChatUser, ChatKey);
					   }
					   string response = await session.SendAsync(e.GetArg("sentence"));
					   await e.Channel.SendMessage(response);
				   }
				   catch (Exception error) { Console.WriteLine($"[ERROR] An issue occured while trying to +chat: {error.ToString()}"); }
			   });

		_client.GetService<CommandService>().CreateGroup("test", cgb =>
		{
			cgb.CreateCommand("save")
				.Description("Multi-server data test")
				.Parameter("data", ParameterType.Unparsed)
				.Do(async e =>
				{
					//Save("data", e.GetArg("data"), e.Server.Id, 1);
					//await e.Channel.SendMessage($"data saved");
					await e.Channel.SendMessage($"no");
				});

			cgb.CreateCommand("load")
				.Description("Multi-server data test")
				.Parameter("line", ParameterType.Required)
				.Do(async e =>
				{
					try
					{
						/* int i = 0; // line number
					   bool success = int.TryParse(e.GetArg("line"), out i); // output line number to line number
					   if (success) // check if line number was parsed successfully
					   {
						   string data = Load("data", e.Server.Id, i); // run Load with required data
						   if (data != null) // check if operation was successful
						   await e.Channel.SendMessage(data); // output line
					   else if it failed...
							   await e.Channel.SendMessage("file/line didnt exist"); // ...then say it failed
					   }
					   else
					   {
						   await e.Channel.SendMessage($"failed to parse input ({e.GetArg("line")})"); // input wasn't an int
					   }*/

						await e.Channel.SendMessage($"no");
					}
					catch (Exception error)
					{
						await e.Channel.SendMessage($"error: {error.ToString()}");
					}
				});
		});

		_client.Ready += (s, e) =>
		{
			Console.WriteLine($"[{DateTime.UtcNow.Hour}:{DateTime.UtcNow.Minute}:{DateTime.UtcNow.Second}] Connected as {_client.CurrentUser.Name}#{_client.CurrentUser.Discriminator}");
		};

		_client.ExecuteAndWait(async () =>
		{
			string token = File.ReadAllLines("logins.txt")[0];
			await _client.Connect(token, TokenType.Bot);
		});
	}
	public void SaveLine(string filename, string data, ulong server, int linenumber)
	{
		var sepchar = Path.DirectorySeparatorChar; // get operating system's directory seperation character
		var path = $"{Directory.GetCurrentDirectory() + sepchar + server.ToString() + sepchar}"; // get data save directory
		var datafile = $"{path + filename}.txt"; // get config file
		Directory.CreateDirectory(path); // create directory

		StringBuilder newconfig = new StringBuilder(); // create empty "text file" in memory

		if (File.Exists(datafile)) // checks if data file exists
		{
			if (new FileInfo(datafile).Length >= 2) // checks if data file has data
			{
				string[] config = File.ReadAllLines(datafile); // read all lines of the text file
				int currentline = 1; // set current line in the text file
				foreach (String line in config)
				{
					if (currentline == linenumber) { newconfig.Append(data + Environment.NewLine); } // replace line 1 of data with custom stuff
					else { newconfig.Append(line + Environment.NewLine); } // add other lines to file
					currentline++; // increase line number
				}
				if (linenumber - config.Length == 1) { newconfig.Append(data + Environment.NewLine); }
			}

			else { newconfig.Append(data + Environment.NewLine); } // file has nothing so just add data to first line
		}

		else { newconfig.Append(data + Environment.NewLine); } // file doesn't exist so create it with input

		File.WriteAllText(datafile, newconfig.ToString()); // save changes to file
	}
	public string LoadLine(string filename, ulong server, int line)
	{
		line -= 1;
		var sepchar = Path.DirectorySeparatorChar; // Grab the current operating system's seperation char. (eg. windows: \, linux: /)
		var path = $"{Directory.GetCurrentDirectory() + sepchar + server.ToString() + sepchar}"; // get directory of data file
		var datafile = $"{path + filename}.txt"; // get data file
		Directory.CreateDirectory(path); // create directory if it doesn't exist
		if (File.Exists(datafile)) // check if file exists
		{
			var config = File.ReadAllLines(datafile); // read config
			if (!string.IsNullOrWhiteSpace(config[line]))
				return config[line]; // return line
			else
				return null;
		}
		else
		{
			return null; // can't load if file doesn't exist
		}
	}
	public void SaveFile(string filename, string data, ulong server)
	{
		var sepchar = Path.DirectorySeparatorChar; // get operating system's directory seperation character
		var path = $"{Directory.GetCurrentDirectory() + sepchar + server.ToString() + sepchar}"; // get data save directory
		var datafile = $"{path + filename}.txt"; // get config file
		Directory.CreateDirectory(path); // create directory

		File.WriteAllText(datafile, data); // save changes to file
	}
	public string[] LoadFile(string filename, ulong server)
	{
		var sepchar = Path.DirectorySeparatorChar; // Grab the current operating system's seperation char. (eg. windows: \, linux: /)
		var path = $"{Directory.GetCurrentDirectory() + sepchar + server.ToString() + sepchar}"; // get directory of data file
		var datafile = $"{path + filename}.txt"; // get data file
		Directory.CreateDirectory(path); // create directory if it doesn't exist
		if (File.Exists(datafile)) // check if file exists
		{
			var config = File.ReadAllLines(datafile); // read config
			if (config.Length > 0)
				return config; // return file
			return null; // file had no lines
		}
		return null; // can't load if file doesn't exist
	}
	public void Clear(string filename, ulong server)
	{
		var sepchar = Path.DirectorySeparatorChar; // get operating system's directory seperation character
		var path = $"{Directory.GetCurrentDirectory() + sepchar + server.ToString() + sepchar}"; // get data save directory
		var datafile = $"{path + filename}data.txt"; // get config file
		Directory.CreateDirectory(path); // create directory
		File.WriteAllText(datafile, null);
	}
	public bool IsMTWOWChannel(ulong server, ulong channel)
	{
        ulong mtChannel = 000000000000000000;
        string data = LoadLine("data", server, 1);
        bool parseResult = ulong.TryParse(data, out mtChannel);
		if (parseResult)
		{
			if (channel == mtChannel)
				return true;
			return false;
		}
		return false;
	}
    public static void VoteCount(ulong server, string twow, int elim, int prize) {//framework is here, modify as needed
        string counter = ConfigurationManager.AppSettings.Get("Counter");//change these settings in App.config
        string python = ConfigurationManager.AppSettings.Get("PyPath");


        ProcessStartInfo myProcessStartInfo = new ProcessStartInfo(python);
 
        myProcessStartInfo.UseShellExecute = false;
        myProcessStartInfo.RedirectStandardOutput = true;

        myProcessStartInfo.Arguments = counter + " "+server+"/"+twow+" -e "+elim+" -t "+prize;

        Process myProcess = new Process();
        myProcess.StartInfo = myProcessStartInfo;
        myProcess.Start();
       
        
        myProcess.WaitForExit();
        myProcess.Close();
    }
    public static void GenerateBooksona(ulong server, string name) {
        string bookMaker = ConfigurationManager.AppSettings.Get("BookMaker");//change these settings in App.config
        string python = ConfigurationManager.AppSettings.Get("PyPath");


        ProcessStartInfo myProcessStartInfo = new ProcessStartInfo(python);

        myProcessStartInfo.UseShellExecute = false;
        myProcessStartInfo.RedirectStandardOutput = true;

        myProcessStartInfo.Arguments = bookMaker + " " + server + "/booksonas " + name;

        Process myProcess = new Process();
        myProcess.StartInfo = myProcessStartInfo;
        myProcess.Start();


        myProcess.WaitForExit();
        myProcess.Close();
    }

    public static String sha256_hash(String value)//for the killswitch
    {
        using (SHA256 hash = SHA256Managed.Create())
        {
            return String.Concat(hash
              .ComputeHash(Encoding.UTF8.GetBytes(value))
              .Select(item => item.ToString("x2")));
        }
    }
}
