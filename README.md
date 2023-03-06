## BrowserSnatch
Gather credentials and cookies from Chrome based browsers on Windows
- built using .NET 6 but will run on Windows 10/11 where .NET ~4 is present by default

![scrnsht1](./Images/screenshot1.png)

#### Targets
- Chrome
- Edge
- Opera

## Build
Install .NET 6
```
sudo apt install dotnet-sdk-6.0
```

Required Packages
```
dotnet add package System.Data.SQLite
dotnet add package System.Text.Json
```

Publish
```
dotnet publish -r win-x64 -p:PublishSingleFile=true --self-contained true -p:PublishTrimmed=true
```

Files Created
```
SharpBrowserSnatch.exe
SQLite.Interop.dll
```

#### File Locations (Google example)
Key used to encrypt passwords/cookies
```
C:\Users\admin\AppData\Local\Google\Chrome\User Data\Local State
```

Encrypted login credentials
```
C:\Users\admin\AppData\Local\Google\Chrome\User Data\Default\Login Data
```

Encrypted cookies
```
C:\Users\admin\AppData\Local\Google\Chrome\User Data\Default\Network\Cookies
```
#### To Do
- [ ] Clean
- [ ] Fix Format for Date/Time "Last Used"
- [ ] Firefox support

#### Resources that inspired the project
- [Unit 42: Credential Gathering From Third-Party Software](https://unit42.paloaltonetworks.com/credential-gathering-third-party-software/#post-124901-_uoyret6muycd)
    - [Example Code: Python Passwords](https://www.thepythoncode.com/article/extract-chrome-passwords-python)
    - [Example Code: Pyhton Cookies](https://www.thepythoncode.com/article/extract-chrome-cookies-python)


![leafy](./Images/Leafy.jpg)
