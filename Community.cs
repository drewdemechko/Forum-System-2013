using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Linq;
using System.Data;
using BlazeGames.Web.Core;
using MySql.Data.MySqlClient;
using System.Text.RegularExpressions;

namespace BlazeGames.Web
{
    public class DynamicPage : CodePage
    {
		private DataSet dataSet = null;
		
        public override void onPageInitialize()
        {
			dataSet = new DataSet("Community");
			LoadTables("boards", "threads", "posts", "likes");
			
			int Id = 0;
			
			if(Utilities.GET("CreateThread") == "true" && int.TryParse(Utilities.GET("ParentId"), out Id))
			{
				int MinimumAuthority = 0, Member = LoggedInMember.ID, ParentId = Id;
				string Title = Utilities.GET("Title"), Data = Utilities.GET("Data"), Tag = Utilities.GET("Tag");
				CreateThread(MinimumAuthority, ParentId, Member, Tag, Title, Data);		
			}
			
			if(Utilities.GET("CreatePost") == "true" && int.TryParse(Utilities.GET("ParentId"), out Id))
			{
				int ParentId = Id, Member = LoggedInMember.ID;
				string Data = Utilities.GET("Data");
				CreatePost(ParentId, Member, Data);
			}
				
			if(Utilities.GET("CreateLike") == "true" && int.TryParse(Utilities.GET("ParentId"), out Id))
			{
				int ParentId = Id, Member = LoggedInMember.ID;
				bool IsComment = Utilities.GET("IsComment") == "0" ? false : true;
				CreateLike(ParentId, Member, IsComment);
			}
			
			if(Utilities.GET("FetchLikes") == "true" && int.TryParse(Utilities.GET("ParentId"), out Id))
			{
				int ParentId = Id;
				bool IsComment = Utilities.GET("IsComment") == "0" ? false : true;
				string[] Likers = FetchLikers(ParentId, IsComment);
					
				foreach(string Liker in Likers)
					Http.Response.Write(Liker + "<br />");
					
				Http.Response.End();
			}
			
			if(Utilities.GET("EditPost") == "true" && int.TryParse(Utilities.GET("Id"), out Id))
			{
				bool IsComment = Utilities.GET("IsComment") == "0" ? false : true;
				string Data = Utilities.GET("Data");				
				EditPost(Id, IsComment, Data);
			}
			
			if(Utilities.GET("TogglePost") == "true" && int.TryParse(Utilities.GET("Id"), out Id))
			{
				bool IsComment = Utilities.GET("IsComment") == "0" ? false : true;				
				TogglePost(Id, IsComment);
			}
			
			if(Utilities.GET("ToggleLock") == "true" && int.TryParse(Utilities.GET("Id"), out Id))
				ToggleLock(Id);
			
			if(Utilities.GET("AutoBump") == "true" && int.TryParse(Utilities.GET("Id"), out Id))
				AutoBump(Id);
		}
		
        public override void onPageLoad()
        {			
            if(Utilities.GET("Administration") == "true" && LoggedInMember.Authority >= 50)
			{
				echo("Admin Panel");
				int MinimumAuthority = 0, ParentId = 0;
				
				if(Utilities.GET("CreateCatagory") == "true" && int.TryParse(Utilities.GET("MinimumAuthority"), out MinimumAuthority))
				{
					string Title = Utilities.GET("Title"),Description = Utilities.GET("Description");	
					CreateCategory(MinimumAuthority, Title, Description);
				}
				
				if(Utilities.GET("CreateBoard") == "true" && int.TryParse(Utilities.GET("MinimumAuthority"), out MinimumAuthority) && int.TryParse(Utilities.GET("ParentId"), out ParentId))
				{
					string Title = Utilities.GET("Title"), Description = Utilities.GET("Description");
					CreateBoard(MinimumAuthority, ParentId, Title, Description);
				}
				
			}
			else if(Utilities.GET("Moderation") == "true" && LoggedInMember.Authority >= 2)
			{
				//view member data
			}
			else
			{
				int CatagoryId = 0;
				
				if(int.TryParse(Utilities.GET("Catagory"), out CatagoryId))
					LoadCatagory(CatagoryId);
				else
				{
					DataRow[] Catagories = FetchRows("boards", "IsCatagory=true");
					
					foreach(DataRow catagory in Catagories)
						LoadCatagory((int)(catagory["Id"]));
				}
			}
        }
		
		/*
			This loads a catagory and all it's children. This is most likely the meat
			of the entire program since it actually echo's out the designs and so forth.
		*/
		public void LoadCatagory(int Id)
		{
			string NaviString = @"<a href = '/Community/'>Home</a>";
			
			Catagory catagory = new Catagory(FetchRow("boards", "Id = " + Id + " AND IsCatagory=true"));
			catagory.LoadChildren(dataSet);
			
			string BoardString = Utilities.GET("Board");
			
			//displays children of catagory
			if(BoardString.Length != 0 && Id != 0)
			{
				//gets the last child of a board
				int[] Boards = BoardString.Split('.').Select(i => int.Parse(i)).ToArray();
				Board board = catagory.bdChildren.First(b => b.Id == Boards[0]);
				
				if(board != null)
				{
					string tmpBoardUrl = "/Community/Catagory-" + catagory.Id + "/Board-" + board.Id + "";
					NaviString += string.Format("&nbsp;&nbsp;&nbsp;<a href='{0}'>{1}</a>", tmpBoardUrl, board.Title);
					
					for(int i = 1; i < Boards.Length; i++)
					{
						try { board = board.bdChildren.First(b => b.Id == Boards[i]); } catch { ErrorManager.Error("Invalid board."); }
						
						if(i == Boards.Length - 1)
							tmpBoardUrl += "." + board.Id + "/";
						else
							tmpBoardUrl += "." + board.Id;
						
						NaviString += string.Format("&nbsp;&nbsp;&nbsp;<a href='{0}'>{1}</a>", tmpBoardUrl, board.Title);
					}
					
					//show thread
					int ThreadId = 0;
					if(int.TryParse(Utilities.GET("Thread"), out ThreadId)) 
					{
						
						Thread thread = null;
						try { thread = board.tdChildren.First(t => t.Id == ThreadId); } catch { ErrorManager.Error("Invalid thread."); }
						
						if(thread != null)
						{	
							NaviString += string.Format("&nbsp;&nbsp;&nbsp;<a href='{0}'>{1}</a>", "./", thread.Title);
							echo(string.Format(Design.Navigation, NaviString));
								
							string ThreadUrl = "";
								
							Member Author = new Member(thread.Member, SqlConnection);
							Author.Load();
								
							echo(string.Format(Design.Catagory, ThreadUrl, System.Security.SecurityElement.Escape(thread.Title), ""));
								
							int PostCount = 1;
								
							string ThreadData = System.Security.SecurityElement.Escape(thread.Data);
							ThreadData = PrepareString(ThreadData);
								
							if(thread.IsVisible || LoggedInMember.Authority > 1)
								echo(string.Format(Design.Post,
												  
								Author.Nickname,																																			//author nickname 			{0}
								ThreadData,																																					//post data					{1}
								"/Member/ID-" + Author.ID + "/",																															//author URL				{2}
								thread.DateCreated,																																			//post date					{3}
								thread.lkChildren.Count,																																	//like count				{4}
								ThreadUrl,																																					//post url					{5}
								"#"+PostCount,																																				//post # in thread			{6}
								thread.Id,																																					//post id					{7}	
								0,																																							//iscomment					{8}
								thread.lkChildren.Any(l => l.Member == LoggedInMember.ID) ? "Unlike" : "Like",																				//like button text			{9}
								LoggedInMember.ID == Author.ID || LoggedInMember.Authority > 1 ? "Edit" : "",																				//edit link					{10}
								thread.IsVisible ? "rgba(0,0,0,0.0)" : "rgba(255,0,0,0.3)",																									//background color			{11}
								LoggedInMember.ID == Author.ID || LoggedInMember.Authority > 1 ? 																							//start show/hide button	{12}
										thread.IsVisible ? 
										"<img src='http://i48.tinypic.com/rtnksm.png' height='20' width='20' onmouseover='this.style.cursor=\"pointer\"' onclick='toggleVisibility("+thread.Id+", 0);'/>" :
										"<img src='http://i49.tinypic.com/281e7sw.png' height='20' width='20' onmouseover='this.style.cursor=\"pointer\"' onclick='toggleVisibility("+thread.Id+", 0);'/>" 
										: "",																																				//end show/hide button										
								LoggedInMember.Authority > 1 ? thread.IsLocked ? 																											//lock and unlock			{13}
							    		"<img src='http://i45.tinypic.com/2wqr72s.png' height='20' width='20' onmouseover='this.style.cursor=\"pointer\"' onclick='toggleLock("+thread.Id+");'/>" : 
										"<img src='http://i46.tinypic.com/2lk4b5z.png' height='20' width='20' onmouseover='this.style.cursor=\"pointer\"' onclick='toggleLock("+thread.Id+");'/>" : "",
								LoggedInMember.ID == Author.ID ? "<img src='http://i48.tinypic.com/n6tij6.png' height='20' width='20' onmouseover='this.style.cursor=\"pointer\"' onclick='autoBump("+thread.Id+");'/>" : "" //autobump					{14}
								 ));
							
							//show each post in thread
							foreach(Post post in thread.ptChildren)
							{
								PostCount ++;
								
								Member Poster = new Member(post.Member, SqlConnection);
								Poster.Load();
										
								string PostUrl = "";
								
								string PostData = System.Security.SecurityElement.Escape(post.Data);
								PostData = PrepareString(PostData);
								
								if(post.IsVisible || LoggedInMember.Authority > 1)
									echo(string.Format(Design.Post,
													   
									Poster.Nickname,																//author nickname 			{0}
									PostData,																		//post data					{1}
									"/Member/ID-" + Poster.ID + "/",												//author URL				{2}
									post.Date,																		//post date					{3}
									post.lkChildren.Count,															//like count				{4}
									PostUrl,																		//post url					{5}
									"#"+PostCount,																	//post # in thread			{6}
									post.Id,																		//post id					{7}	
									1,				   																//iscomment					{8}
									post.lkChildren.Any(l => l.Member == LoggedInMember.ID) ? "Unlike" : "Like",	//like button text			{9}
									LoggedInMember.ID == Poster.ID || LoggedInMember.Authority > 1 ? "Edit" : "",	//edit link					{10}
									post.IsVisible ? "rgba(0,0,0,0.0)" : "rgba(255,0,0,0.3)",						//background color			{11}
									LoggedInMember.ID == Poster.ID || LoggedInMember.Authority > 1 ? 				//start show/hide button	{12}
										post.IsVisible ? 
											"<img src='http://i48.tinypic.com/rtnksm.png' height='20' width='20' onmouseover='this.style.cursor=\"pointer\"' onclick='toggleVisibility("+post.Id+", 1);'/>" :
											"<img src='http://i49.tinypic.com/281e7sw.png' height='20' width='20' onmouseover='this.style.cursor=\"pointer\"' onclick='toggleVisibility("+post.Id+", 1);'/>" 
										: "", 																		//end show/hide button		
									"",																				//lock and unlock			{13}
									""																				//autobump					{14}
									));
							}
								
							if(thread.IsVisible || LoggedInMember.Authority > 1)
							{
								echo(string.Format(Design.Reply, thread.Id, thread.IsLocked ? "placeholder='This thread is currently locked.' disabled" : "", thread.IsLocked ? "disabled" : ""));
								IncrementViewCount(thread.Id, thread.Views);
							}
							else
								ErrorManager.Error("Invalid thread.");
								
							echo(Design.CatagoryEnd);
						}
					}
					else //shows board
					{
						echo(string.Format(Design.Navigation, NaviString));
						
						string CatagoryUrl = "/Community/Catagory-" + catagory.Id + "/Board-";
						
						for(int i = 0; i < Boards.Length; i++)
							if(i != (Boards.Length - 1))
								CatagoryUrl += Boards[i] + ".";
							else
								CatagoryUrl += board.Id + "/";
						
						if(Utilities.GET("New") == "true")	
							echo(string.Format(Design.NewThread, board.Id));
						else
						{				
							echo(string.Format(Design.Catagory, CatagoryUrl, board.Title, board.Description));
					
							foreach(Board bd in board.bdChildren)
							{
								int CommentCount = 0;
								foreach(Thread thread in bd.tdChildren)
									CommentCount += thread.ptChildren.Count(v => v.IsVisible && thread.IsVisible);
								
								Thread LatestThread = null;
								Member LatestPoster = null;
								
								if(bd.tdChildren.Count(v => v.IsVisible || LoggedInMember.Authority > 1) > 0)
								{
									LatestThread = bd.tdChildren.OrderByDescending(t => t.DateBumped).Where(p => p.IsVisible).FirstOrDefault();
									
									if(LatestThread != null)
									{
										if(LatestThread.ptChildren.Count > 0)
										{
											Post LatestPost = LatestThread.ptChildren.OrderByDescending(p => p.Date).Where(p => p.IsVisible && LatestThread.IsVisible).FirstOrDefault();
											
											if(LatestPost != null)
											{
												LatestPoster = new Member(LatestPost.Member, SqlConnection);
												LatestPoster.Load();
											}
										}
										else
										{
											LatestPoster = new Member(LatestThread.Member, SqlConnection);
											LatestPoster.Load();
										}
									}
								}
								
								string SubBoardUrl = "/Community/Catagory-" + catagory.Id + "/Board-";
								
								for(int i = 0; i < Boards.Length; i++)
									   SubBoardUrl += Boards[i] + ".";
									
									SubBoardUrl += bd.Id + "/";
									
								echo(string.Format(Design.Board, SubBoardUrl, bd.Title, bd.Description, bd.tdChildren.Count( v => v.IsVisible ), CommentCount, 
												   LatestThread != null ? SubBoardUrl + "Thread-" + LatestThread.Id + "/" : "./",  
												   LatestThread != null ? LatestThread.Title : "None", 
												   LatestPoster != null ? "/Member/ID-" + LatestPoster.ID + "/" : "./",
												   LatestPoster != null ? LatestPoster.Nickname : "", 
												   LatestThread != null ? LatestThread.DateBumped : ""));
							}
							echo(Design.CatagoryEnd);
							
							bool IsStickyThreads = false;
							
							foreach(Thread thread in board.tdChildren)
								if(thread.IsSticky && (thread.IsVisible || LoggedInMember.Authority > 1))
									IsStickyThreads = true;
							
							if(IsStickyThreads)
							{
								echo(string.Format(Design.Catagory, "./", "Sticky Threads", ""));
								
								foreach(Thread thread in board.tdChildren.OrderByDescending(x => DateTime.Parse(x.DateBumped)))
								{
									if(thread.IsSticky)
									{
										Member Author = new Member(thread.Member, SqlConnection);
										Author.Load();
										
										Member LatestPoster = null;
										
										if(thread.ptChildren.Count > 0)
										{
											Post LatestPost = thread.ptChildren.OrderByDescending(t => t.Date).Where(p => p.IsVisible && thread.IsVisible).FirstOrDefault();
											
											if(LatestPost != null)
											{
												LatestPoster = new Member(LatestPost.Member, SqlConnection);
												LatestPoster.Load();
											}
										}
										
										string ThreadUrl = "/Community/Catagory-" + catagory.Id + "/Board-";
										
										for(int i = 0; i < Boards.Length; i++)
											if(i != (Boards.Length - 1))
												ThreadUrl += Boards[i] + ".";
											else
												ThreadUrl += board.Id + "/";
											
										ThreadUrl += "Thread-" + thread.Id + "/";
										
										if(thread.IsVisible || LoggedInMember.Authority > 1)
										echo(string.Format(Design.Thread, ThreadUrl, thread.Title, "/Member/ID-" + Author.ID + "/", Author.Nickname, thread.DateCreated, thread.ptChildren.Count( v => v.IsVisible ),
														  LatestPoster != null ? "/Member/ID-" + LatestPoster.ID + "/" : "./",
														  LatestPoster != null ? LatestPoster.Nickname : "", thread.Views, thread.DateBumped, thread.IsVisible ? "rgba(0,0,0,0.0)" :"rgba(255,0,0,0.3)"));
										
										IsStickyThreads = true;
									}
								}
								
								echo(Design.CatagoryEnd);
							}
							
							
							echo(string.Format(Design.Catagory, "./", "Threads",
											   "<input type='submit' class='CreateButton' name='btn_Submit_New_Thread' value='Create New Thread'onClick=\"location.href='"+CatagoryUrl+"New/'\"/>"));
							
							if(board.tdChildren.Count(v => v.IsVisible || LoggedInMember.Authority > 1) > 0)
							{
								foreach(Thread thread in board.tdChildren.OrderByDescending(x => DateTime.Parse(x.DateBumped)))
								{
									if(!thread.IsSticky)
									{
										Member Author = new Member(thread.Member, SqlConnection);
										Author.Load();
										
										Member LatestPoster = null;
										
										if(thread.ptChildren.Count > 0)
										{
											Post LatestPost = thread.ptChildren.OrderByDescending(t => t.Date).Where(p => p.IsVisible && thread.IsVisible).FirstOrDefault();
											
											if(LatestPost != null)
											{
												LatestPoster = new Member(LatestPost.Member, SqlConnection);
												LatestPoster.Load();
											}
										}
										
										string ThreadUrl = "/Community/Catagory-" + catagory.Id + "/Board-";
										
										for(int i = 0; i < Boards.Length; i++)
											if(i != (Boards.Length - 1))
												ThreadUrl += Boards[i] + ".";
											else
												ThreadUrl += board.Id + "/";
											
										ThreadUrl += "Thread-" + thread.Id + "/";
										
										if(thread.IsVisible || LoggedInMember.Authority > 1)
										echo(string.Format(Design.Thread, ThreadUrl, thread.Title, "/Member/ID-" + Author.ID + "/", Author.Nickname, thread.DateCreated, thread.ptChildren.Count( v => v.IsVisible ),
														  LatestPoster != null ? "/Member/ID-" + LatestPoster.ID + "/" : "./",
														  LatestPoster != null ? LatestPoster.Nickname : "", thread.Views, thread.DateBumped, thread.IsVisible ? "rgba(0,0,0,0.0)" :"rgba(255,0,0,0.3)"));
									}
								}
							}
							else
							{
								echo("<br /><div id = 'smallspace'></div> <div style='text-align:center;'>No Threads!</div><div id = 'smallspace'></div>");
							}
							
							echo(Design.CatagoryEnd);
						}
					}
				}
			}
			else //shows catagory
			{
				echo(string.Format(Design.Navigation, NaviString));
				echo(string.Format(Design.Catagory, "/Community/Catagory-" + catagory.Id +"/", catagory.Title, catagory.Description));
				
				
				foreach(Board board in catagory.bdChildren)
				{
					int CommentCount = 0;
					foreach(Thread thread in board.tdChildren)
						if(thread.IsVisible)
							CommentCount += thread.ptChildren.Count( v => v.IsVisible );
					
					Thread LatestThread = null;
					Member LatestPoster = null;
					
					if(board.tdChildren.Count > 0)
					{
						LatestThread = board.tdChildren.OrderByDescending(t => t.DateBumped).Where(p => p.IsVisible).FirstOrDefault();
						
						if(LatestThread != null)
						{
							
							if(LatestThread.ptChildren.Count(p => p.IsVisible) > 0)
							{
								Post LatestPost = LatestThread.ptChildren.OrderByDescending(p => p.Date).Where(p => p.IsVisible && LatestThread.IsVisible).FirstOrDefault();
								
								if(LatestPost != null)
								{
									LatestPoster = new Member(LatestPost.Member, SqlConnection);
									LatestPoster.Load();
								}
							}
							else
							{
								LatestPoster = new Member(LatestThread.Member, SqlConnection);
								LatestPoster.Load();
							}
						}
					}
					
					
					echo(string.Format(Design.Board, "/Community/Catagory-" + catagory.Id + "/Board-" + board.Id +"/", board.Title, board.Description, board.tdChildren.Count( v => v.IsVisible ), CommentCount, 
									   LatestThread != null ? "/Community/Catagory-" + catagory.Id + "/Board-" + board.Id + "/Thread-" + LatestThread.Id + "/" : "./",  
									   LatestThread != null ? LatestThread.Title : "None", 
									   LatestPoster != null ? "/Member/ID-" + LatestPoster.ID + "/" : "./",
									   LatestPoster != null ? LatestPoster.Nickname : "", 
									   LatestThread != null ? LatestThread.DateBumped : ""));
				}
				
				echo(Design.CatagoryEnd);
			}
		}
		
		/*
			This method loads all the specified tables from the database.
		*/
		public void LoadTables(params string[] TableName)
		{
			foreach(string Name in TableName)
			{
				MySqlDataAdapter dataAdapter = new MySqlDataAdapter();
				MySqlCommand FetchTable = new MySqlCommand("SELECT * FROM " + Name, SqlConnection);
				
				dataAdapter.TableMappings.Add("Table", Name);
				dataAdapter.SelectCommand = FetchTable;
				dataAdapter.Fill(dataSet);
			}
		}
		
		/*
			This method selects a single data row from a data table where the condition matches.
		*/
		public DataRow FetchRow(string TableName, string Condition)
		{
			return dataSet.Tables[TableName].Select(Condition)[0];
		}
		
		/*
			This method selects multiple data rows from a data table where the condition matches.
		*/
		public DataRow[] FetchRows(string TableName, string Condition)
		{
			return dataSet.Tables[TableName].Select(Condition);
		}
		
		/*
			This method creates a board inside of a specified catagory.
		*/
		public void CreateBoard(int MinimumAuthority, int ParentId, string Title, string Description)
		{
			if(LoggedInMember.Authority >= 2)
			{
				MySqlCommand cmdCreateBoard = new MySqlCommand(@"INSERT INTO boards (MinimumAuthority, ParentId, IsCatagory, IsLocked, IsVisible, Title, Description) 
				VALUES (@MinimumAuthority, @ParentId,  @IsCatagory, @IsLocked, @IsVisible, @Title, @Description)", SqlConnection);
				
				cmdCreateBoard.Parameters.AddWithValue("@MinimumAuthority", MinimumAuthority);
				cmdCreateBoard.Parameters.AddWithValue("@ParentId", ParentId);
				cmdCreateBoard.Parameters.AddWithValue("@IsCatagory", false);
				cmdCreateBoard.Parameters.AddWithValue("@IsLocked", false);
				cmdCreateBoard.Parameters.AddWithValue("@IsVisible", true);
				cmdCreateBoard.Parameters.AddWithValue("@Title", Title);
				cmdCreateBoard.Parameters.AddWithValue("@Description", Description);
				
				cmdCreateBoard.ExecuteNonQuery();
			}
		}
		
		/*
			This method creates a catagory on the /Community/ page.
		*/
		public void CreateCategory(int MinimumAuthority, string Title, string Description)
		{
			if(LoggedInMember.Authority >= 2)
			{
				MySqlCommand cmdCreateCategory = new MySqlCommand(@"INSERT INTO boards (MinimumAuthority, ParentId, Title, Description, IsCategory, IsLocked, IsVisible)
				VALUES (@MinimumAuthority, @ParentId, @Title, @Description, @IsCategory, @IsLocked, @IsVisible)", SqlConnection);
				
				cmdCreateCategory.Parameters.AddWithValue("@MinimumAuthority", MinimumAuthority);
				cmdCreateCategory.Parameters.AddWithValue("@ParentId", 0);
				cmdCreateCategory.Parameters.AddWithValue("@Title", Title);
				cmdCreateCategory.Parameters.AddWithValue("@Description", Description);
				cmdCreateCategory.Parameters.AddWithValue("@IsCategory", true);
				cmdCreateCategory.Parameters.AddWithValue("@IsLocked", false);
				cmdCreateCategory.Parameters.AddWithValue("@IsVisible", true);
				
				cmdCreateCategory.ExecuteNonQuery();
			}
		}
		
		/*
			This method creates a thread in the specified board.
		*/
		public void CreateThread(int MinimumAuthority, int ParentId, int Member, string Tag, string Title, string Data)
		{
			if(Title.Length != 0 && Data.Length != 0)
			{
				MySqlCommand cmdCreateThread = new MySqlCommand(@"INSERT INTO threads (MinimumAuthority, ParentId, DateBumped, DateCreated, Member, Tag, Title, Data, IsLocked, IsSticky, IsVisible)
				VALUES (@MinimumAuthority, @ParentId, @DateBumped, @DateBumped,@Member, @Tag, @Title, @Data, @IsLocked, @IsSticky, @IsVisible)", SqlConnection);
				
				cmdCreateThread.Parameters.AddWithValue(@"MinimumAuthority", MinimumAuthority);
				cmdCreateThread.Parameters.AddWithValue(@"ParentId", ParentId);
				cmdCreateThread.Parameters.AddWithValue(@"DateBumped", DateTime.Now);
				cmdCreateThread.Parameters.AddWithValue(@"DateCreated", DateTime.Now);
				cmdCreateThread.Parameters.AddWithValue(@"Member", Member);
				cmdCreateThread.Parameters.AddWithValue(@"Tag", Tag);
				cmdCreateThread.Parameters.AddWithValue(@"Title", Title);
				cmdCreateThread.Parameters.AddWithValue(@"Data", Data);
				cmdCreateThread.Parameters.AddWithValue(@"IsLocked", false);
				cmdCreateThread.Parameters.AddWithValue(@"IsSticky", false);
				cmdCreateThread.Parameters.AddWithValue(@"IsVisible", true);
				
				cmdCreateThread.ExecuteNonQuery();
			}
		}
		
		/* 
			This method creates a post on a thread. It features double-posting prevention and automatically appends
			the second post onto the first post to successfully foil those spammers.
		*/
		public void CreatePost(int ParentId, int Member, string Data)
		{
			MySqlCommand cmdFetchVisibility = new MySqlCommand("SELECT IsVisible FROM threads WHERE Id=@Id", SqlConnection);
			cmdFetchVisibility.Parameters.AddWithValue("@Id", ParentId);
			bool IsVisible = (bool)cmdFetchVisibility.ExecuteScalar();
			
			MySqlCommand cmdFetchLock = new MySqlCommand("SELECT IsLocked FROM threads WHERE Id=@Id", SqlConnection);
			cmdFetchLock.Parameters.AddWithValue("@Id", ParentId);
			bool IsLocked = (bool)cmdFetchLock.ExecuteScalar();
			
			if(IsVisible && !IsLocked)
			{
				if(Data.Length > 3)
				{
					MySqlCommand cmdFetchLastMember = new MySqlCommand(@"SELECT Member FROM posts WHERE ParentId=@ParentId ORDER BY Id DESC LIMIT 1", SqlConnection);		
					cmdFetchLastMember.Parameters.AddWithValue("@ParentId", ParentId);
					
					Int32 LastMember = Convert.ToInt32(cmdFetchLastMember.ExecuteScalar());
	
					if(LastMember != Member)
					{
						MySqlCommand cmdCreatePost = new MySqlCommand(@"INSERT INTO posts (ParentId, Member, Data, IsVisible, Date)
						VALUES (@ParentId, @Member, @Data, @IsVisible, @Date)", SqlConnection);
						
						cmdCreatePost.Parameters.AddWithValue("@ParentId", ParentId);
						cmdCreatePost.Parameters.AddWithValue("@Member", Member);
						cmdCreatePost.Parameters.AddWithValue("@Data", Data);
						cmdCreatePost.Parameters.AddWithValue("@IsVisible", true);
						cmdCreatePost.Parameters.AddWithValue("@Date", DateTime.Now);
						
						cmdCreatePost.ExecuteNonQuery();
					}
					else
					{
						MySqlCommand cmdFetchLastId = new MySqlCommand(@"SELECT MAX(Id) FROM posts WHERE ParentId=@ParentId", SqlConnection);
						cmdFetchLastId.Parameters.AddWithValue("@ParentId", ParentId);
						Int32 LastId = Convert.ToInt32(cmdFetchLastId.ExecuteScalar());
						
						MySqlCommand cmdFetchData = new MySqlCommand(@"SELECT Data FROM posts WHERE Id=@Id", SqlConnection);
						cmdFetchData.Parameters.AddWithValue("@Id", LastId);
						string OldData = (string)cmdFetchData.ExecuteScalar();
						
						MySqlCommand cmdUpdatePost = new MySqlCommand(@"UPDATE posts SET Data=@Data WHERE Id=@Id", SqlConnection);
						cmdUpdatePost.Parameters.AddWithValue("@Id", LastId);
						cmdUpdatePost.Parameters.AddWithValue("@Data", OldData + "[br /][br /][small][em]Auto Merged Post "+DateTime.Now+" [/em][/small][br /][br /]" + Data);
						cmdUpdatePost.ExecuteNonQuery();
					}
					
					MySqlCommand cmdUpdateThread = new MySqlCommand(@"UPDATE threads SET DateBumped=@DateBumped WHERE Id=@Id", SqlConnection);
					cmdUpdateThread.Parameters.AddWithValue("@DateBumped", DateTime.Now);
					cmdUpdateThread.Parameters.AddWithValue("@Id", ParentId);
					
					cmdUpdateThread.ExecuteNonQuery();
				}
			}
		}
		
		/*
			This method adds a like for a member on a specified post or thread. If the
			member has already liked, the like is removed.
		*/
		public void CreateLike(int ParentId, int Member, bool IsComment)
		{																						
			MySqlCommand cmdFetchLike = new MySqlCommand(@"SELECT * FROM likes WHERE ParentId=@ParentId AND Member=@Member AND IsComment=@IsComment", SqlConnection);
			cmdFetchLike.Parameters.AddWithValue("@ParentId", ParentId);
			cmdFetchLike.Parameters.AddWithValue("@Member", Member);
			cmdFetchLike.Parameters.AddWithValue("@IsComment", IsComment);
			
			bool HasLiked = false;
			
			using(MySqlDataReader rdrFetchLike = cmdFetchLike.ExecuteReader())
				if(rdrFetchLike.Read())
					HasLiked = true;
				
			if(HasLiked)
			{
				MySqlCommand cmdRemoveLike = new MySqlCommand(@"DELETE FROM likes WHERE ParentId=@ParentId AND Member=@Member AND IsComment=@IsComment", SqlConnection);
				cmdRemoveLike.Parameters.AddWithValue("@ParentId", ParentId);
				cmdRemoveLike.Parameters.AddWithValue("@Member", Member);
				cmdRemoveLike.Parameters.AddWithValue("@IsComment", IsComment);
				
				cmdRemoveLike.ExecuteNonQuery();
			}
			else
			{
				MySqlCommand cmdCreateLike = new MySqlCommand(@"INSERT INTO likes (ParentId, Member, IsComment)
					VALUES (@ParentId, @Member, @IsComment)", SqlConnection);
				
				cmdCreateLike.Parameters.AddWithValue("@ParentId", ParentId);
				cmdCreateLike.Parameters.AddWithValue("@Member", Member);
				cmdCreateLike.Parameters.AddWithValue("@IsComment", IsComment);
				
				cmdCreateLike.ExecuteNonQuery();
			}
		}
		
		/*
			This method returns a list of people who have liked the specified thread or post so 
			that it can be displayed in a popup box.
		*/
		public string[] FetchLikers(int ParentId, bool IsComment)
		{
			MySqlCommand cmdFetchLikers = new MySqlCommand(@"SELECT Member FROM likes where ParentId=@ParentId AND IsComment=@IsComment", SqlConnection);
			cmdFetchLikers.Parameters.AddWithValue("@ParentId", ParentId);
			cmdFetchLikers.Parameters.AddWithValue("@IsComment", IsComment);
			
			List<string> Likers = new List<string>();
			List<int> LikerIds = new List<int>();
			
			using(MySqlDataReader rdrFetchLikers = cmdFetchLikers.ExecuteReader())
				while(rdrFetchLikers.Read())
					LikerIds.Add(rdrFetchLikers.GetInt32("Member"));
				
			foreach(int MemberId in LikerIds)
			{
				Member member = new Member(MemberId, SqlConnection);
				member.Load();
				
				Likers.Add(member.Nickname);
			}
				
			return Likers.ToArray();
		}
		
		/*
			This method updates the text of a post without bumping the thread. Only the
			author of the post or moderator and above can edit posts.
		*/		
		public void EditPost(int Id, bool IsComment, string Data)
		{
			if(IsComment)
			{
				MySqlCommand cmdFetchAuthor = new MySqlCommand("SELECT Member FROM posts WHERE Id=@Id", SqlConnection);
				cmdFetchAuthor.Parameters.AddWithValue("@Id", Id);		
				int AuthorId = (int)cmdFetchAuthor.ExecuteScalar();
				
				if(AuthorId == LoggedInMember.ID || LoggedInMember.Authority > 1)
				{
					MySqlCommand cmdEditPost = new MySqlCommand("UPDATE posts SET Data=@Data WHERE Id=@Id", SqlConnection);
					cmdEditPost.Parameters.AddWithValue("@Id", Id);
					cmdEditPost.Parameters.AddWithValue("@Data", Data);
					cmdEditPost.ExecuteNonQuery();
				}
			}
			else
			{
				MySqlCommand cmdFetchAuthor = new MySqlCommand("SELECT Member FROM threads WHERE Id=@Id", SqlConnection);
				cmdFetchAuthor.Parameters.AddWithValue("@Id", Id);		
				int AuthorId = (int)cmdFetchAuthor.ExecuteScalar();
				
				if(AuthorId == LoggedInMember.ID || LoggedInMember.Authority > 1)
				{
					MySqlCommand cmdEditThread = new MySqlCommand("UPDATE threads SET Data=@Data WHERE Id=@Id", SqlConnection);
					cmdEditThread.Parameters.AddWithValue("@Id", Id);
					cmdEditThread.Parameters.AddWithValue("@Data", Data);
					cmdEditThread.ExecuteNonQuery();
				}
			}
		}
		
		/*
			This method is used for soft deletion. Posts and threads are only shown to regular users
			if they are visibile. Anyone moderator and above will still see the posts in red signifying
			that the thread has been deleted.
		*/
		public void TogglePost(int Id, bool IsComment)
		{
			MySqlCommand cmdFetchAuthor = new MySqlCommand("SELECT Member FROM " + (IsComment ? "posts" : "threads") + " WHERE Id=@Id", SqlConnection);
			cmdFetchAuthor.Parameters.AddWithValue("@Id", Id);		
			int AuthorId = (int)cmdFetchAuthor.ExecuteScalar(); 
			
			MySqlCommand cmdFetchVisibility = new MySqlCommand("SELECT IsVisible FROM " + (IsComment ? "posts" : "threads") + " WHERE Id=@Id", SqlConnection);
			cmdFetchVisibility.Parameters.AddWithValue("@Id", Id);
			bool IsVisible = (bool)cmdFetchVisibility.ExecuteScalar();
			
			MySqlCommand cmdSetVisibility = new MySqlCommand("UPDATE " + (IsComment ? "posts" : "threads") + " SET IsVisible=@IsVisible WHERE Id=@Id", SqlConnection);
			cmdSetVisibility.Parameters.AddWithValue("@Id", Id);
			cmdSetVisibility.Parameters.AddWithValue("@IsVisible", 
			
			IsVisible ? 
				LoggedInMember.Authority > 1 || LoggedInMember.ID == AuthorId ?
					!IsVisible
					: 
					IsVisible 
				:
				LoggedInMember.Authority > 1 ? 
					!IsVisible 
			 : 
			 IsVisible);
			
			cmdSetVisibility.ExecuteNonQuery();
		}
		
		/*
			This method will set the state of a thread to locked if it is unlocked and vice versa.
			This can only be done by a moderator.
		*/
		public void ToggleLock(int Id)
		{
			if(LoggedInMember.Authority > 1)
			{
				MySqlCommand cmdFetchLock = new MySqlCommand("SELECT IsLocked FROM threads WHERE Id=@Id", SqlConnection);
				cmdFetchLock.Parameters.AddWithValue("@Id", Id);
				bool IsLocked = (bool)cmdFetchLock.ExecuteScalar();

				MySqlCommand cmdSetLock = new MySqlCommand("UPDATE threads SET IsLocked=@IsLocked WHERE Id=@Id", SqlConnection);
				cmdSetLock.Parameters.AddWithValue("@Id", Id);
				cmdSetLock.Parameters.AddWithValue("@IsLocked", IsLocked ? 0 : 1);
				cmdSetLock.ExecuteNonQuery();
			}
		}
		
		/*
			This method is called every time a thread is accessed. All it does is increment the thread
			view count stored in the database.
		*/
		public void IncrementViewCount(int Id, int Count)
		{
			MySqlCommand cmdSetViewCount = new MySqlCommand("UPDATE threads SET Views=@Count WHERE Id=@Id", SqlConnection);
			cmdSetViewCount.Parameters.AddWithValue("@Id", Id);
			cmdSetViewCount.Parameters.AddWithValue("@Count", Count+1);
			cmdSetViewCount.ExecuteNonQuery();
		}
		
		/*
			This method updates the bump date of a thread to now. At the board level the threads are sorted
			by the bump date so by executing this method you will bring the thread to the top of the list.
		*/
		public void AutoBump(int Id)
		{
			MySqlCommand cmdGetBumpDate = new MySqlCommand("SELECT DateBumped FROM threads WHERE Id=@Id", SqlConnection);
			cmdGetBumpDate.Parameters.AddWithValue("@Id", Id);
			DateTime BumpDate = (DateTime)cmdGetBumpDate.ExecuteScalar();
			
			TimeSpan timeSpan = DateTime.Now.Subtract(BumpDate);
			
			if(timeSpan.Days >= 1)
			{
				MySqlCommand cmdSetBumpDate = new MySqlCommand("UPDATE threads SET DateBumped=@Now WHERE Id=@Id", SqlConnection);
				cmdSetBumpDate.Parameters.AddWithValue("@Id", Id);
				cmdSetBumpDate.Parameters.AddWithValue("@Now", DateTime.Now);
				cmdSetBumpDate.ExecuteNonQuery();
				
				Http.Response.Write("Your thread has been bumped to the top of the list.");
				Http.Response.End();
			}
			else
			{
				Http.Response.Write("You can only bump your thread once every 24 hours.");
				Http.Response.End();
			}
		}
		
		/*
			This method sets IsSticky to !IsSticky for a thread. If the thread is stickied, it 
			will appear in a section above the normal threads always on top.
		*/
		public void ToggleSticky(int Id)
		{
			if(LoggedInMember.Authority > 1)
			{
				MySqlCommand cmdGetIsSticky = new MySqlCommand("SELECT IsSticky FROM threads WHERE Id=@Id", SqlConnection);
				cmdGetIsSticky.Parameters.AddWithValue("@Id", Id);
				bool IsSticky = (bool)cmdGetIsSticky.ExecuteScalar();
				
				MySqlCommand cmdSetIsSticky = new MySqlCommand("UPDATE threads SET IsSticky=@IsSticky WHERE Id=@Id", SqlConnection);
				cmdSetIsSticky.Parameters.AddWithValue("@Id", Id);
				cmdSetIsSticky.Parameters.AddWithValue("@IsSticky", IsSticky ? 0 : 1);
				cmdSetIsSticky.ExecuteNonQuery();
			}
		}
		
		/*
			This method replaces BBCode tags with HTML. At the moment if a closing tag is missing 
			it will get appended to the end of the post to prevent it from interfering with
			other threads. This should most likely be redone in this future to use REGEX.
		*/
		public string PrepareString(string Data)
		{
			Data = Data.Replace("[b]", "<b>").Replace("[/b]", "</b>");
			Data = Data.Replace("[u]", "<u>").Replace("[/u]", "</u>");
			Data = Data.Replace("[em]", "<em>").Replace("[/em]", "</em>");
			Data = Data.Replace("[small]", "<small>").Replace("[/small]", "</small>");
			Data = Data.Replace("[br /]", "<br />");
			Data = Regex.Replace(Data, @"\[color=((.|\n)*?)(?:\s*)\]((.|\n)*?)\[/color(?:\s*)\]", "<span style=\"color:$1;\">$3</span>");
			
			int StartTagBoldCount = (Data.Length - Data.Replace("<b>", "").Length) / 3;
			int EndTagBoldCount = (Data.Length - Data.Replace("</b>", "").Length) / 3;
			int StartTagUnderlineCount = (Data.Length - Data.Replace("<u>", "").Length) / 3;
			int EndTagUnderlineCount = (Data.Length - Data.Replace("</u>", "").Length) / 3;
			int StartTagItalCount = (Data.Length - Data.Replace("<em>", "").Length) / 4;
			int EndTagItalCount = (Data.Length - Data.Replace("</em>", "").Length) / 4;
			int StartTagSmallCount = (Data.Length - Data.Replace("<small>", "").Length) / 7;
			int EndTagSmallCount = (Data.Length - Data.Replace("</small>", "").Length) / 7;
			
			if(StartTagBoldCount != EndTagBoldCount)
				if(StartTagBoldCount > EndTagBoldCount)
					for(int i = 0; i < StartTagBoldCount - EndTagBoldCount; i++)
						Data += "</b>";
					
			if(StartTagUnderlineCount != EndTagUnderlineCount)
				if(StartTagUnderlineCount > EndTagUnderlineCount)
					for(int i = 0; i < StartTagUnderlineCount - EndTagUnderlineCount; i++)
						Data += "</u>";
			
			if(StartTagItalCount != EndTagItalCount)
				if(StartTagItalCount > EndTagItalCount)
					for(int i = 0; i < StartTagItalCount - EndTagItalCount; i++)
						Data += "</em>";
			
			if(StartTagSmallCount != EndTagSmallCount)
				if(StartTagSmallCount > EndTagSmallCount)
					for(int i = 0; i < StartTagSmallCount - EndTagSmallCount; i++)
						Data += "</small>";
				
			return Data;
		}
	}
	
	
	public class Design
	{
		public const string
			Catagory = @"
<div class = 'category'>
	<div class = 'categorytitle'><a href='{0}'>{1}</a></div>
	{2}
	
",
			CatagoryEnd = @"
</div><div class = 'categoryfooter'></div><div id = 'smallspace'></div>
",
			Board = @"
<div class = 'board'>
	<div class = 'icon'>
		<img src = 'http://i48.tinypic.com/2ed1tkw.png' />
	</div>
		
	<div class = 'leftboard'>
		<div style='float:right;font-size:9px;'>{2}</div>
		<div class = 'title'><a href = '{0}'> {1}</a></div>
		
		<div class = 'threadcount'>Threads: {3}</div>
		<div class = 'commentcount'>Comments: {4}</div>
	</div>
		
	<div class = 'rightboard'>
		<div class = 'latestarticle'>Latest:<a href = '{5}'> {6}</a></div>
<div class = 'latestposter'><a href = '{7}'> {8}</a>,</div>
<div class = 'timestamp'> {9}</div>
	</div>	
</div>

<div id = 'smallspace'></div>
",
			Thread = @"
<div class = 'board' style='background-color:{10}'>
	<div class = 'profilepicture'>
		<img src = 'http://i50.tinypic.com/lilv5.png' />
	</div>

	<div class = 'leftboard'>
		<div class = 'title'><a href = '{0}'>{1}</a></div>
		<div class = 'author'><a href = '{2}'>{3}</a>,</div>
		<div class = 'timestampsubforumlevelleft'>{4}</div>
	</div>

	<div class = 'rightboard'>
		<div class = 'replycount'>Replies: {5}</div>
		<div class = 'latestpostersubforumlevel'><a href = '{6}'>{7}</a></div>
		<div class = 'viewcount'>Views: {8}</div>
		<div class = 'timestampsubforumlevelright'>{9}</div>
	</div>
	
</div>
<div id = 'smallspace'></div>
",
			Post = @"
<div class = 'boardthreadlevel' style='background-color:{11};'>
	<div class = 'profilecontainer'>
		<div class = 'profilepicturethreadlevel'>
			<img src = 'http://i47.tinypic.com/30x7o1v.png' />
		</div>
		<div class = 'nameplate'>
			<div class = 'user'>{0}</div>
		</div>
	</div>

	<div class = 'postcontainer'>
		<div class = 'hideandunhide'>
			{12}
		</div>
		<div class='iconSep'></div>
		<div class = 'lockandunlock'>
			{13}
		</div>
		<div class='iconSep'></div>
		<div class = 'autobump'>
			{14}
		</div>
		<div class = 'post' id='post{7}'>{1}</div>
	</div>

	<div class = 'leftboardthreadlevel'>
		<div class = 'poster'><a href = '{2}'>{0}</a>,</div>
		<div class = 'timestampthreadlevel'>{3}</div>
		<br />
		<div class = 'likecount'>Likes: <a onmouseover='this.style.cursor=""pointer""' onclick='popUpLikes({7}, {8});'>{4}</a></div>
		<input class = 'likebutton' type = 'submit' value = '{9}' onClick='addLike({7}, {8})' />
	</div>

	<div class = 'rightboardthreadlevel'>
		<div class = 'permalink' style='float:left;'><a onmouseover='this.style.cursor=""pointer""' onclick= ""doEditAlert(this, {7}, {8});"">{10}</a></div>
		<div class = 'permalink'><a href = '{5}'>{6}</a></div>
	</div>
</div>
<div id = 'smallspace'></div>
",
			Reply = @"
<textarea style='margin-top: 3px; margin-left:12px; color: #999; background-image: -moz-radial-gradient(left center, ellipse farthest-side, #444 0%, #333 100%); background-image: -ms-radial-gradient(left center, ellipse farthest-side, #444 0%, #333 100%); resize: none; width: 1320px;height:70px;' id='Reply' rows='2' {1}></textarea>
<input id = 'replybutton' type = 'submit' value = 'Reply' onClick='addPost({0})' {2} />
",
			NewThread = @"
		<!--Container-->
		<div id='Title_Tag_Container' style='margin-bottom: 10px;margin-top:30px;'>
		
		<!--TagDiv-->
		<div>
		<div style='color:#999;float:left;margin-right:10px;'>
		<strong>Tag:</strong>
		</div>
		<div>
		<input id='Tag' style='background-image: -moz-radial-gradient(left center, ellipse farthest-side, #444 0%, #333 100%); background-image: -ms-radial-gradient(left center, ellipse farthest-side, #444 0%, #333 100%); color:#999; width:145px; float: left; margin-right:15px;' type='text' maxlength='20' name='Tag'/>
		<label style='color:red;float:left;margin-right:10px;' id='ErrorTag'></label>
		</div>
		</div>
		

		<!--TitleDiv-->
		<div>
		<div style='color:#999;float:left; margin-right: 6px;'>
		<strong>Title:</strong>
		</div>
		<div>						
		<input id='Title' style='background-image: -moz-radial-gradient(left center, ellipse farthest-side, #444 0%, #333 100%); background-image: -ms-radial-gradient(left center, ellipse farthest-side, #444 0%, #333 100%); color:#999; width: 500px; margin-right: 15px;' type='text' maxlength='50' name='Title'/> <!--1081-->
		<label  style='color:red;' id='ErrorTitle'></label>
		</div>
		</div>


		</div>	<!--END Title_Tag_Container-->

		<!--1416px width of container-->

		<!--PostDiv-->
		<div style='color:#999;margin-bottom:10px'>
		<div style='float:left; margin-right: 5px;'>
			<strong>Post:</strong> 
		</div>
		<div>
		<textarea id='Post' style='color: #999; background-image: -moz-radial-gradient(left center, ellipse farthest-side, #444 0%, #333 100%); background-image: -ms-radial-gradient(left center, ellipse farthest-side, #444 0%, #333 100%); resize: none; width: 1366px;' name='Post' rows='43'></textarea>
		</div>
		</div>

		<div>
<input type='submit' name='btn_Submit_New_Thread' onclick='addThread({0})'; value='Create New Thread' style='font-weight: bold; color: #999; background-image: -moz-radial-gradient(center, circle closest-corner, #4D4DFF 0%, #00F 100%); -ms-radial-gradient(center, circle closest-corner, #4D4DFF 0%, #00F 100%); float:right;'/>
		</div>
",
			Navigation = @"
<div class = 'menubar'>
	<div class = 'navbar'>
		{0} 
	</div>
	
	<div class = 'searchbar'>
		<input id = 'searchtextfield' type = 'text' value = 'Search is disabled!' name = 'search' disabled />
		<input id = 'searchbutton' type = 'submit' value = 'Search' disabled />
	</div>
</div>
";
	}
	
	/*
		This is a class to load a catagory object on the board.
	*/
	public class Catagory
	{
		public bool
			IsVisible;
		
		public int
			Id,
			MinimumAuthority;
		
		public string
			Description,
			Title;
		
		public List<Board> bdChildren = new List<Board>();
		
		public Catagory(DataRow dataRow)
		{
			IsVisible = (bool)dataRow["IsVisible"];
			
			Id = (int)dataRow["Id"];
			MinimumAuthority = (int)dataRow["MinimumAuthority"];
			
			Description = (string)dataRow["Description"];
			Title = (string)dataRow["Title"];
		}
		
		public void LoadChildren(DataSet dataSet)
		{
			DataRow[] Rows = dataSet.Tables["boards"].Select("ParentId=" + Id + " AND IsCatagory=false");
			
			foreach(DataRow Row in Rows)
			{
				Board board = new Board(Row);
				board.LoadChildren(dataSet);
				bdChildren.Add(board);
			}
		}
	}
	
	/*
		This is a class to load a board object on the board.
	*/
	public class Board
	{
		public bool
			IsLocked,
			IsVisible;
		
		public int
			Id,
			MinimumAuthority,
			ParentId;
		
		public string
			Description,
			Title;
		
		public List<Board> bdChildren = new List<Board>();
		public List<Thread> tdChildren = new List<Thread>();
		
		public Board(DataRow dataRow)
		{
			IsLocked = (bool)dataRow["IsLocked"];
			IsVisible = (bool)dataRow["IsVisible"];
			
			Id = (int)dataRow["Id"];
			MinimumAuthority = (int)dataRow["MinimumAuthority"];
			ParentId = (int)dataRow["ParentId"];
			
			Description = (string)dataRow["Description"];
			Title = (string)dataRow["Title"];
		}
		
		public void LoadChildren(DataSet dataSet)
		{
			DataRow[] bdRows = dataSet.Tables["boards"].Select("ParentId=" + Id + " AND IsCatagory=false");
			
			foreach(DataRow Row in bdRows)
			{
				Board board = new Board(Row);
				board.LoadChildren(dataSet);
				bdChildren.Add(board);
			}
			
			DataRow[] tdRows = dataSet.Tables["threads"].Select("ParentId=" + Id);
			
			foreach(DataRow Row in tdRows)
			{
				Thread thread = new Thread(Row);
				thread.LoadChildren(dataSet);
				tdChildren.Add(thread);
			}
		}
	}
	
	/*
		This is a class to load a thread object on the board.
	*/
	public class Thread
	{
		public bool
			IsLocked,
			IsSticky,
			IsVisible;
		
		public int 
			Id,
			Member,
			MinimumAuthority,
			ParentId,
			Views;
		
		public string
			Data,
			DateBumped,
			DateCreated,
			Tag,
			Title;
		
		public List<Like> lkChildren = new List<Like>();
		public List<Post> ptChildren = new List<Post>();
		
		public Thread(DataRow dataRow)
		{
			IsLocked = (bool)dataRow["IsLocked"];
			IsSticky = (bool)dataRow["IsSticky"];
			IsVisible = (bool)dataRow["IsVisible"];
			
			Id = (int)dataRow["Id"];
			Member = (int)dataRow["Member"];
			MinimumAuthority = (int)dataRow["MinimumAuthority"];
			ParentId = (int)dataRow["ParentId"];
			Views = (int)dataRow["Views"];
			
			Data = (string)dataRow["Data"];
			DateBumped = (string)dataRow["DateBumped"].ToString();
			DateCreated = (string)dataRow["DateCreated"].ToString();
			Title = (string)dataRow["Title"];
			Tag = (string)dataRow["Tag"];
		}
		
		public void LoadChildren(DataSet dataSet)
		{
			DataRow[] lkRows = dataSet.Tables["likes"].Select("ParentId=" + Id + " AND IsComment=false");
			
			foreach(DataRow Row in lkRows)
			{
				Like like = new Like(Row);
				lkChildren.Add(like);
			}
			
			DataRow[] ptRows = dataSet.Tables["posts"].Select("ParentId=" + Id);
			
			foreach(DataRow Row in ptRows)
			{
				Post post = new Post(Row);
				post.LoadChildren(dataSet);
				ptChildren.Add(post);
			}
		}
	}
	
	/*
		This is a class to load a post object on the board.
	*/
	public class Post
	{
		public bool
			IsVisible;
		
		public int
			Id,
			Member,
			ParentId;
		
		public string
			Data,
			Date;
		
		public List<Like> lkChildren = new List<Like>();
		
		public Post(DataRow dataRow)
		{
			IsVisible = (bool)dataRow["IsVisible"];
			
			Id = (int)dataRow["Id"];
			Member = (int)dataRow["Member"];
			ParentId = (int)dataRow["ParentId"];
			
			Data = (string)dataRow["Data"];
			Date = (string)dataRow["Date"].ToString();
		}
		
		public void LoadChildren(DataSet dataSet)
		{
			DataRow[] lkRows = dataSet.Tables["likes"].Select("ParentId=" + Id + " AND IsComment=true");
			
			foreach(DataRow Row in lkRows)
			{
				Like like = new Like(Row);
				lkChildren.Add(like);
			}
		}
	}
	
	/*
		This is a class to load a like object on the board.
	*/
	public class Like
	{
		public int
			Id,
			Member,
			ParentId;
		
		public Like(DataRow dataRow)
		{
			Id = (int)dataRow["Id"];
			Member = (int)dataRow["Member"];
			ParentId = (int)dataRow["ParentId"];
		}
	}
}