﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;

namespace wowarmory.Network {
    public class ChatModule {
        public delegate void OnMessageDelegate(ChatModule module, Chat.Message message);
        public delegate void OnPresenceDelegate(ChatModule module, Chat.Presence presence);
        public delegate void OnChatLoggedInOutDelegate();
        public delegate void OnLoginFailedDelegate(string reason);

        public event OnMessageDelegate OnMessageMOTD;
        public event OnMessageDelegate OnMessageGuildChat;
        public event OnMessageDelegate OnMessageOfficerChat;
        public event OnMessageDelegate OnMessageWhisper;
        public event OnChatLoggedInOutDelegate OnChatLoggedIn;
        public event OnChatLoggedInOutDelegate OnChatLoggedOut;

        public event OnPresenceDelegate OnPresenceChange;

        public event OnLoginFailedDelegate OnLoginFailed;

        Session session;

        string chatSessionId;
        string name, realm;

        System.Timers.Timer keepAliveTimer = new System.Timers.Timer(120000);
        
        public ChatModule(Session session, string name, string realm) {
            this.session = session;
            this.name = name;
            this.realm = realm;

            keepAliveTimer.Elapsed += new ElapsedEventHandler(keepAliveTimer_Elapsed);

            session.OnResponseReceived += new Connection.OnResponseReceivedDelegate(OnResponseReceived);
            session.OnSessionEstablished += new Session.OnSessionEstablishedDelegate(OnSessionEstablished);
            session.OnSessionClosed += new Session.OnSessionClosedDelegate(OnSessionClosed);
            session.OnError += new Session.OnErrorDelegate(OnError);
        }

        void OnError(Response response) {
            if ((response.Target == "/chat-disconnect" || response.Target == "/chat-login") && OnLoginFailed != null)
                OnLoginFailed((string)response["body"]);
        }


        void OnSessionClosed(string reason) {
            
        }

        public void Close() {
            var request = new Request("/chat-logout");
            request["chatSessionId"] = chatSessionId;
            session.Connection.SendRequest(request);
        }

        void keepAliveTimer_Elapsed(object sender, ElapsedEventArgs e) {
            Console.WriteLine("Sending 'keep-alive'");

            var request = new Request("/ah-mail");
            request["r"] = realm;
            request["cn"] = name;
            session.Connection.SendRequest(request);
        }

    
        void OnSessionEstablished() {
            var request = new Request("/chat-login");
            var options = new Dictionary<string, object>();
            options["matureFilter"] = "false";
            request["options"] = options;
            request["n"] = name;
            request["r"] = realm;
            session.Connection.SendRequest(request);

            keepAliveTimer.Start();
        }


        void OnResponseReceived(Response response) {
            if (response.Target == "/chat-logout") {
                if (OnChatLoggedOut != null)
                    OnChatLoggedOut();
            } else if (response.Target == "/chat-login") {
                Console.WriteLine("Logged into chat");
                chatSessionId = (string)response["chatSessionId"];

                if (OnChatLoggedIn != null)
                    OnChatLoggedIn();
            } else  if (response.Target == "/chat") {
                var chatType = (string)response["chatType"];
                if (chatType == "message_ack") {
                    Console.WriteLine("received ack");
                    return;
                }
                var from = (Dictionary<string, object>)response["from"];
                if (chatType == "wow_message") {
                    var message = new Chat.Message(response);

                    if (message.Type == Chat.Message.CHAT_MSG_TYPE_GUILD_MOTD) {
                        if (OnMessageMOTD != null)
                            OnMessageMOTD(this, message);
                    } else if (message.Type == Chat.Message.CHAT_MSG_TYPE_GUILD_CHAT) {
                        if (OnMessageGuildChat != null)
                            OnMessageGuildChat(this, message);
                    } else if (message.Type == Chat.Message.CHAT_MSG_TYPE_WHISPER) {
                        if (OnMessageWhisper != null)
                            OnMessageWhisper(this, message);
                    } else if (message.Type == Chat.Message.CHAT_MSG_TYPE_OFFICER_CHAT) {
                        if (OnMessageOfficerChat != null)
                            OnMessageOfficerChat(this, message);
                    } else {
                        Console.WriteLine("unhandled message type: " + message.Type);
                    }
                } else if (chatType == "wow_presence") {
                    var presence = new Chat.Presence(response);

                    if (OnPresenceChange != null)
                        OnPresenceChange(this, presence);
                } else {
                    Console.WriteLine("unhandled chat type: " + chatType);
                }
            }
        }

        public void SendMessage(string msg, string chatType = Chat.Message.CHAT_MSG_TYPE_GUILD_CHAT) {
            var request = new Request("/chat-guild");
            request["type"] = chatType;
            request["body"] = msg;
            request["chatSessionId"] = chatSessionId;
            session.Connection.SendRequest(request);
        }

        public void SendWhisper(string toCharId, string msg) {
            var request = new Request("/chat-whisper");
            request["to"] = toCharId;
            request["body"] = msg;
            request["chatSessionId"] = chatSessionId;
            session.Connection.SendRequest(request);
        }

        
    }
}
