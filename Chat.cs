using Microsoft.AspNetCore.SignalR;
using System.Collections.Generic;
using System.Threading.Tasks;



public class ChatHub : Hub
{
    private static List<string> ConnectedUsers = new List<string>();

    public async Task RegisterUser(string user)
    {
        ConnectedUsers.Add(user);
        await Clients.All.SendAsync("UserListUpdated", ConnectedUsers);
    }

    public async Task SendMessage(List<Message> msg) //Nigde se ne zove, tako da nista ne radi
    {

        try
        {
            await Clients.All.SendAsync("ReceiveMessage", msg); //Treba ovo da se koristi za slanje poruka pa da pozovem funkciju iz HomeController, ali mi nije radilo
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error in SendMessage: {ex.Message}");
            throw;
        }
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var user = Context.User.Identity.Name;
        await LogoutUser(user);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task LogoutUser(string user)
    {
        ConnectedUsers.Remove(user);
        await Clients.All.SendAsync("UserListUpdated", ConnectedUsers);
    }   
}

