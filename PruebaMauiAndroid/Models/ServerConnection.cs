﻿
using Android.Webkit;
using System.Net.Sockets;
using System.Text;



namespace PruebaMauiAndroid.Models
{
    class ServerConnection
    {
        private static string ip;
        private static int port;
        public static string token;
        public static UserInfo ConnectedUser;

        public enum Protocol
        {
            LOGIN = 1,
            USER = 2,
            ERROR = 9
        }
        public enum ServerErrorActions
        {
            NOT_ALLOWED = 112,
            DATA_BORN_GREATER_NOW = 1105,
            FORMAT_ERROR_PACKET = 101,
            USER_NAME_ALREADY_EXISTS = 1202,
            USER_NAME_IS_EMPTY = 1201,
            FULLNAME_IS_EMPTY = 1203,
            LOGIN_FAILED = 109,
            ALREADY_CONNECTED = 113,
            UNKNOWN_ERROR = 9

        }
        public enum ClientLoginActions {
        LOGIN = 1,
        LOGOUT = 2,
        SHUTDOWN_SERVER = 5

        }
        public enum ClientUserActions {
        CHANGE_PASSWORD = 1,
        CHANGE_FULLNAME = 2,
        CHANGE_BORN_DATE = 3,
        CHANGE_OTHER_DATA = 4,
        GET_USER_INFO = 5

        }
        public enum ServerLoginActions
        {
            LOGIN_SUCCESS = 3,
            LOGIN_FAILED = 2,
            RESET_PASSWORD_AFTER_LOGOUT = 4,
        

        }
        public enum ServerUserActions
        { 
            USER_INFO = 15,
            CANVI_NOM = 20,
            CANVI_DATA = 21,
            CANVI_ALTRES = 22


        }

        //UTILITY & SETUP

        public ServerConnection(string ip, int port)
        {

            ServerConnection.ip = ip;
            ServerConnection.port = port;


        }
        public static async Task<string> SendDataAsync(string dataToSend, int timeoutMilliseconds = 5000)
        {
            try
            {

                using TcpClient tcpClient = new()
                {
                    NoDelay = true
                };



                using var cts = new CancellationTokenSource(timeoutMilliseconds);

                Task connectTask = tcpClient.ConnectAsync(ip, port);
                if (await Task.WhenAny(connectTask, Task.Delay(timeoutMilliseconds, cts.Token)) != connectTask)
                {
                    throw new TimeoutException("Se agotó el tiempo de espera.");
                }

                using NetworkStream networkStream = tcpClient.GetStream();
                networkStream.WriteTimeout = timeoutMilliseconds;
                networkStream.ReadTimeout = timeoutMilliseconds;

                //Añadimos fin de linea para que el server pueda leer los datos
                byte[] data = Encoding.UTF8.GetBytes(dataToSend + "\n");

                await networkStream.WriteAsync(data, 0, data.Length);

                await networkStream.FlushAsync();

                byte[] responseBuffer = new byte[1024];
                int bytesRead = await networkStream.ReadAsync(responseBuffer, 0, responseBuffer.Length, cts.Token);

                string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);


                return response;
            }
            catch (Exception ex)
            {

                return $"Error: {ex.Message}";
            }
        }
        //TODO: Aquí encriptaremos el mensaje hacia el servidor.
        public static string ConstructServerMessage(string protocol, string action, List<string> dataToSend)
        {
            string message = protocol + action;

            foreach (var entry in dataToSend)
            {


                message += string.Format("{0:D2}", entry.Length) + entry;
            }


            return message;


        }

        public static Dictionary<string, string> ParseData(List<string> keys, string data)
        {

            Dictionary<string, string> parsedData = [];

            foreach (var k in keys)
            {

                if (int.TryParse(data[..2], out int size))
                {

                    string extracted = data.Substring(2, size);

                    parsedData[k] = extracted;

                    data = data[(2 + size)..];

                }


            }


            return parsedData;

        }



        //HANDLE RESPONSES & ACTIONS
        public static bool HandleResponse(string response)
        {

            int parsedProtocol = -1;
            int parsedAction = -1;
            string data;
            int.TryParse(response[..1], out parsedProtocol);

            //Los otros son siempre 2 bytes para la acción pero en el 9 puede haber más (4 creo segun la wiki).
            if (parsedProtocol != 9)
            {

                int.TryParse(response.Substring(1, 2), out parsedAction);
                data = response.Substring(3);
            } else
            {

                int.TryParse(response.Substring(1, 4), out parsedAction);
                data = response.Substring(5);

            }
            System.Console.WriteLine("ParsedProtocol: " + parsedProtocol + "  ||  ParsedAction:" + parsedAction);
            System.Console.WriteLine("DATA: " + data);
            Protocol protocol = (Protocol)parsedProtocol;


            switch (protocol)
            {

                case Protocol.USER:
                    ServerUserActions serverUserAction = (ServerUserActions)parsedAction;
                    return HandleServerUserActions(serverUserAction, data);
                 

                case Protocol.LOGIN:
                    ServerLoginActions serverLoginAction = (ServerLoginActions)parsedAction;
                    return HandleServerLoginActions(serverLoginAction, data);

                case Protocol.ERROR:
                    ServerErrorActions serverErrorAction = (ServerErrorActions)parsedAction;
                    return HandleServerErrorActions(serverErrorAction, data);
                default:
                    Console.WriteLine("Protocolo desconocido.");
                    break;
            }

            return false;

        }

        private static bool HandleServerErrorActions(ServerErrorActions serverAction, string data)
        {

            switch (serverAction)
            {

                case ServerErrorActions.DATA_BORN_GREATER_NOW:
                    Console.WriteLine("La data es major que la data actual.");
                    break;
                case ServerErrorActions.FORMAT_ERROR_PACKET:
                    Console.WriteLine("Error en el formato de paquete enviado al servidor.");
                    break;
                case ServerErrorActions.FULLNAME_IS_EMPTY:
                    Console.WriteLine("El nombre real no puede estar en blanco.");
                    break;
                case ServerErrorActions.LOGIN_FAILED:
                    Console.WriteLine("Los credenciales no son válidos.");
                    break;
                case ServerErrorActions.NOT_ALLOWED:
                    Console.WriteLine("Acción no permitida.");
                    break;
                case ServerErrorActions.UNKNOWN_ERROR:
                    Console.WriteLine("Error desconocodio en el servidor.");
                    break;
                case ServerErrorActions.USER_NAME_ALREADY_EXISTS:
                    Console.WriteLine("El nombre de usuario ya existe.");
                    break;
                case ServerErrorActions.USER_NAME_IS_EMPTY:
                    Console.WriteLine("El nombre de usuario no puede estar vacio.");
                    break;
                case ServerErrorActions.ALREADY_CONNECTED:
                    Console.WriteLine("Ya existe una conexión abierta, hay que hacer logout antes.");
                    break;

                default:
                    Console.WriteLine("Acción desconocida para ERROR_RESPONSES");
                    break;
            }

            return false;
        }

        private static bool HandleServerUserActions(ServerUserActions serverAction, string data)
        {

            switch (serverAction)
            {
                
                case ServerUserActions.USER_INFO:
                    Console.WriteLine("Handling USER INFO");
                    var dict = ParseData(["token", "user", "user2", "realName", "fecha"], data);
                    //Falta el alias pero hay algo raro como numeros de más en el mensaje
                    ConnectedUser.isAdmin = data.Trim().LastOrDefault().ToString() == "1";
                    return true;

                default:
                    Console.WriteLine("Acción desconocida para USER");
                    break;
            }

            return false;


        }

        private static bool HandleServerLoginActions(ServerLoginActions serverAction, string data)
        {


            switch (serverAction)
            {
                case ServerLoginActions.LOGIN_SUCCESS:
                    Console.WriteLine("Handling LOGIN_SUCCESS");
                    var retToken = ServerConnection.ParseData(["token"], data);
                    if (retToken.ContainsKey("token"))
                    {

                        System.Console.WriteLine("TOKEN OBTAINED: " + retToken["token"]);
                        ServerConnection.token = retToken["token"];
                        return true;
                    }
                    break;
                case ServerLoginActions.LOGIN_FAILED:
                    Console.WriteLine("Handling LOGIN_FAILED");
                    //NO HAY TOKEN DE MOMENTO ASI QUE.. return true;
                    return true;
                    var retTokenFailed = ServerConnection.ParseData(["token"], data);
                    if (retTokenFailed.ContainsKey("token"))
                    { 
                            if(ServerConnection.token == retTokenFailed["token"])
                        {
                            ServerConnection.token = null;
                            return true;

                        }
                    }
                        break;
              

                default:
                    Console.WriteLine("Acción desconocida para LOGIN");
                    break;
            }

            return false;
        }



        // DO ACTIONS

        public static async Task<bool> UserLogin(string user, string password)
        {
            ServerConnection.ConnectedUser = new(user);
            string loginData = ServerConnection.ConstructServerMessage(((int)Protocol.LOGIN).ToString(),((int)ClientLoginActions.LOGIN).ToString("D2") , [user, password]);



            var response = await ServerConnection.SendDataAsync(loginData);

            System.Console.WriteLine("SentLogin: " + loginData);
            System.Console.WriteLine("ResponseLogin:" + response);

            var isValidResponse = HandleResponse(response);


            if (isValidResponse)
            {
                await GetUserInfo();
            }


            return isValidResponse;
        }

        public static async Task<bool> UserLogout()
        {



            if (!String.IsNullOrEmpty(ServerConnection.token))
            {

                string dataToSend = ServerConnection.ConstructServerMessage(((int)Protocol.LOGIN).ToString(), ((int)ClientLoginActions.LOGOUT).ToString("D2"), [ServerConnection.token, ConnectedUser.userName]);
                string response = await SendDataAsync(dataToSend);


                System.Console.WriteLine("SentLogout: " + dataToSend);
                System.Console.WriteLine("ResponseLogout:" + response);
                return HandleResponse(response);


            }


            return false;
        }

        public static async Task<bool> GetUserInfo()
        {

            string userInfo = ServerConnection.ConstructServerMessage(((int)Protocol.USER).ToString(), ((int)ClientUserActions.GET_USER_INFO).ToString("D2"), [ServerConnection.token, ConnectedUser.userName, ConnectedUser.userName]);

            var response = await ServerConnection.SendDataAsync(userInfo);



            System.Console.WriteLine("SentGetUserInfo: " + userInfo);
            System.Console.WriteLine("ResponseGetUserInfo" + response);


            return HandleResponse(response);
        }

        
        
        

    }
}
