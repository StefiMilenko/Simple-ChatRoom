using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.SignalR;

public class HomeController : Controller
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly IMemoryCache _memoryCache;

    public HomeController(IMemoryCache memoryCache, IHubContext<ChatHub> hubContext)
    {
        _memoryCache = memoryCache;
        _hubContext = hubContext;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public IActionResult SendMessage([FromBody] Message message)
    {
        try{
            var key = CreateKey(32); 
            message.Key=key;
            var encmsg = Encrypt(message.MessageText, key, message.Mode);
            message.MessageText=encmsg;
            message.MessageEncrypted=encmsg;
            var messages = _memoryCache.GetOrCreate("messages", entry => new List<Message>());
            messages.Add(message);
            _memoryCache.Set("messages", messages);

            return Ok();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error processing message: {ex.Message}");
            return StatusCode(500, "Internal Server Error");
        }
    }

    public async Task<IActionResult> GetMessagesHub() //Sad se ovo koristi
    {   
        try{
            var messages = _memoryCache.Get<List<Message>>("messages") ?? new List<Message>();
            var decryptedMessages = new List<Message>();
            foreach (var msg in messages)
            {
                var decryptedMsg = new Message
                {
                    User = msg.User,
                    Key = msg.Key,
                    MessageText = Decrypt(msg.MessageText, msg.Key, msg.Mode),
                    MessageEncrypted= msg.MessageEncrypted,
                    Mode = msg.Mode
                };
                decryptedMessages.Add(decryptedMsg);
            }
            await _hubContext.Clients.All.SendAsync("ReceiveMessage", decryptedMessages); //poziva ChatHub funkciju
            return Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in GetMessages: {ex}");
            return StatusCode(500); 
        }
    }

    public static string CreateKey(int length)
    {
        using (var rng = new RNGCryptoServiceProvider())
        {
            byte[] keyBytes = new byte[length];
            rng.GetBytes(keyBytes);
            return BitConverter.ToString(keyBytes).Replace("-", "").ToLower();
        }
    }

    /*public static uint[] GenerateIV() //Za CBC
    {
        byte[] ivBytes = new byte[8];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(ivBytes);
        }

        uint[] iv = new uint[2];
        iv[0] = BitConverter.ToUInt32(ivBytes, 0);
        iv[1] = BitConverter.ToUInt32(ivBytes, 4);

        return iv;
    }*/

    public static string Encrypt(string message, string key, int mode)
    {
        //Kodiranje RC4
        byte[] keyBytes = Encoding.UTF8.GetBytes(key);
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        byte[] ciphertextBytes = RC4(keyBytes, messageBytes);
        return BitConverter.ToString(ciphertextBytes).Replace("-", "").ToLower();
        /*else{ //XTEA
            string msg1 = message.Substring(0, message.Length / 2);
            string msg2 = message.Substring(message.Length / 2);
            byte[] msg1Byte = Encoding.UTF8.GetBytes(msg1);
            byte[] msg2Byte = Encoding.UTF8.GetBytes(msg2);
            uint messageUInt1 = BitConverter.ToUInt32(msg1Byte, 0);
            uint messageUInt2 = BitConverter.ToUInt32(msg2Byte, 0);
            uint[] keyUInt = StringToUIntArray(key);
            uint[] K = new uint[4];
            int point = 0;
				for (int i = 0; i < K.Length; i++) //Kljuc u 4 dela
				{
					uint output;
					output =  (keyUInt[point]);
					output += (keyUInt[point + 1] << 8);
					output += (keyUInt[point + 2] << 16);
					output += (keyUInt[point + 3] << 24);					
					point += 4;
					K[i] = output;
                }
                
            uint[] V = new uint[2]; // Poruka u 2
            V[0] = messageUInt1;
            V[1] = messageUInt2;
            return EnXTEA(V, K);
        }*/
    }

    public static string Decrypt(string ciphertext, string key, int mode)
    {
        
        //Dekodiranje
        if (mode ==1 ) { //RC4
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] ciphertextBytes = HexStringToByteArray(ciphertext);
            byte[] decryptedBytes = RC4(keyBytes, ciphertextBytes);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        else { //XTEA
            string msg1 = ciphertext.Substring(0, ciphertext.Length / 2);
            string msg2 = ciphertext.Substring(ciphertext.Length / 2);
            byte[] msg1Byte = Encoding.UTF8.GetBytes(msg1);
            byte[] msg2Byte = Encoding.UTF8.GetBytes(msg2);
            uint messageUInt1 = BitConverter.ToUInt32(msg1Byte, 0);
            uint messageUInt2 = BitConverter.ToUInt32(msg2Byte, 0);
            uint[] keyUInt = StringToUIntArray(key);
            uint[] K = new uint[4];
            int point = 0;
				for (int i = 0; i < K.Length; i++) //Kljuc u 4 dela
				{
					uint output;
					output =  (keyUInt[point]);
					output += (keyUInt[point + 1] << 8);
					output += (keyUInt[point + 2] << 16);
					output += (keyUInt[point + 3] << 24);					
					point += 4;
					K[i] = output;
				}
            uint[] V = new uint[2]; //Poruka u 2
            V[0] = messageUInt1;
            V[1] = messageUInt2;
            return DeXTEA(V, K);
        }
    }

    static byte[] RC4(byte[] key, byte[] data)
    {
        byte[] result = new byte[data.Length]; //output
        byte[] s = new byte[key.Length]; //Lokalni kljuc

        //Popunjavanje kljuca, loop 1   
        for (int k = 0; k < key.Length; k++)
        {
            s[k] = (byte)k;
        }
        //loop 2
        int l = 0;
        for (int k = 0; k < key.Length; k++)
        {
            l = (l + key[k % key.Length] + s[k]) % key.Length;
            Swap(s, k, l);
        }

        int i = 0;
        int j = 0;
        //RC4 algoritam
        for (int k = 0; k < data.Length; k++)
        {
            i = (i + 1) % key.Length;
            j = (j + s[i]) % key.Length;
            Swap(s, i, j);
            byte a = data[k];
			byte b = s[(s[i]+s[j])% key.Length];
            result[k] = (byte)((int)a^(int)b);
        }

        return result;
    }

    static string EnXTEA(uint[] v, uint[] key) 
    {
        uint v0 = v[0], v1 = v[1], sum = 0, delta = 0x9E3779B9;
        Console.WriteLine(v0);
        Console.WriteLine(v1);
        for (int i = 0; i < 32; i++)
        {
            v0 += (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
            sum += delta;
            v1 += (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
        }

        return Convert.ToBase64String(UIntArrayToBytes(new uint[] { v0, v1 }));
    }

    static string DeXTEA(uint[] v, uint[] key) 
    {
        uint v0 = v[0], v1 = v[1], delta = 0x9E3779B9, sum = delta * 32;

        for (int i = 0; i < 32; i++)
        {
            v1 -= (((v0 << 4) ^ (v0 >> 5)) + v0) ^ (sum + key[(sum >> 11) & 3]);
            sum -= delta;
            v0 -= (((v1 << 4) ^ (v1 >> 5)) + v1) ^ (sum + key[sum & 3]);
        }

        return Convert.ToBase64String(UIntArrayToBytes(new uint[] { v0, v1 }));
    }

    static void Swap(byte[] array, int i, int j)
    {
        byte temp = array[i];
        array[i] = array[j];
        array[j] = temp;
    }

    static byte[] HexStringToByteArray(string hex)
    {
        if (hex.Length % 2 != 0)
        {
            throw new ArgumentException("Hex string must have an even number of characters.");
        }

        int numberChars = hex.Length;
        byte[] bytes = new byte[numberChars / 2];

        for (int i = 0; i < numberChars; i += 2)
        {
            if (!byte.TryParse(hex.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i / 2]))
            {
                throw new FormatException($"Invalid hex character at position {i}.");
            }
        }
        return bytes;
    }

    static uint[] StringToUIntArray(string input) 
    {
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        int numUInts = (int)Math.Ceiling((double)bytes.Length / 4);
        uint[] result = new uint[numUInts];

        for (int i = 0; i < numUInts; i++)
        {
            int bytesToCopy = Math.Min(4, bytes.Length - i * 4);
            byte[] tempBytes = new byte[4];
            Array.Copy(bytes, i * 4, tempBytes, 0, bytesToCopy);
            result[i] = BitConverter.ToUInt32(tempBytes, 0);
        }

        return result;
    }

    static string UIntArrayToString(uint[] input)
    {
        byte[] bytes = new byte[input.Length * 4];

        for (int i = 0; i < input.Length; i++)
        {
            byte[] tempBytes = BitConverter.GetBytes(input[i]);
            int bytesToCopy = Math.Min(4, tempBytes.Length);
            Array.Copy(tempBytes, 0, bytes, i * 4, bytesToCopy);
        }

        return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
    }
    static byte[] UIntArrayToBytes(uint[] input)
    {
        byte[] bytes = new byte[input.Length * 4];

        for (int i = 0; i < input.Length; i++)
        {
            byte[] tempBytes = BitConverter.GetBytes(input[i]);
            int bytesToCopy = Math.Min(4, tempBytes.Length);
            Array.Copy(tempBytes, 0, bytes, i * 4, bytesToCopy);
        }
        return bytes;
    }

    static string BytesToHexString(byte[] bytes)
    {
        StringBuilder hexString = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
        {
            hexString.AppendFormat("{0:x2}", b);
        }
        return hexString.ToString();
    }
}