# SharpSilentChrome 

https://github.com/user-attachments/assets/b8595cd8-77e1-41ab-ad72-293e0c78168e

SharpSilentChrome is a C# project that "silently" installs browser extensions on Google Chrome or MS Edge by updating the browsers' `Preferences` and `Secure Preferences` files. Currently, it only supports Windows. 

[Blog post - KOREAN](https://blog.sunggwanchoi.com/kor-introducing-sharpsilentchrome/)

I simply ported Syntax-err0r's and AsaurusRex's python code to C#. For all information regarding research, detection, opsec, and more, refer to the credits section below. 

## Credits 

Original Research Paper: [https://www.cse.chalmers.se/~andrei/cans20.pdf](https://www.cse.chalmers.se/~andrei/cans20.pdf)

Nicholas Murray(Syntax-err0r)'s original blog post: [https://syntax-err0r.github.io/Silently_Install_Chrome_Extension.html](https://syntax-err0r.github.io/Silently_Install_Chrome_Extension.html) and[https://syntax-err0r.github.io/Return_Of_The_Extension.html](https://syntax-err0r.github.io/Return_Of_The_Extension.html)

AsaurusRex's blog post: [https://medium.com/@marcusthebrody/silently-install-chrome-extensions-macos-version-becf164679c2](https://medium.com/@marcusthebrody/silently-install-chrome-extensions-macos-version-becf164679c2) and [https://medium.com/@marcusthebrody/silently-install-macos-chrome-extensions-part-2-c9deab4216cd](https://medium.com/@marcusthebrody/silently-install-macos-chrome-extensions-part-2-c9deab4216cd)

AsaurusRex's python code: [https://github.com/asaurusrex/Silent_Chrome](https://github.com/asaurusrex/Silent_Chrome)

## Usage 

Drop the `extension` directory on target's filesystem 

Standalone usage 
```
Usage: SharpSilentChrome.exe install /browser:[chrome/msedge] /sid:<SID> /profilepath:<user_profile_path> /path:<extension_path>
Usage: SharpSilentChrome.exe revert /browser:[chrome/msedge] /sid:<SID> /profilepath:<user_profile_path>

Example: SharpSilentChrome.exe install /browser:chrome /sid:S-1-5-21-1234567890-1234567890-1234567890-1000 /profilepath:"C:\Users\john.doe" /path:"C:\Users\Public\Downloads\extension"
Example: SharpSilentChrome.exe revert /browser:chrome /sid:S-1-5-21-1234567890-1234567890-1234567890-1000 /profilepath:"C:\Users\john.doe

Path is CASE SENSITIVE
```

Sliver usage 
```
inline-execute-assembly /root/SharpSilentChrome.exe 'install /sid:S-1-5-21-3783789134-3776525684-4265850423-500 /path:C:\Users\Public\Downloads\extension /browser:chrome /profilepath:c:\users\administrator'
```

## Caveats & OPSEC  

1. Before installing the extension, SSC will create backup files for `Preferences` and `Secure Preferences` in user's data directory. 

2. If a user has browser processes running, SSC will kill and restart all browser processes. This will take around 1 second and upon restarting, browser will restore all previous tabs and cookies. However, the user will feel the "process crash" type of experience. 

3. If a user does not have browser processes running, SSC will just simply install the extension. 

4. Installing extension to another/different user will NOT restore their browser process. It is highly recommended to install the extension when the target user is not running their browser process. 

## TODO 

- [ ] BOF port 
- [ ] Update handling different/another user when they have browser processes running. Currently, SSC will not restore their tabs and will not restart the browser - the target user must manually restart the browser from their gui session. 

## CursedChrome testing 
This section is mainly for personal testing purposes using the [CursedChrome](https://github.com/mandatoryprogrammer/CursedChrome) project. You can just ignore this part. 
```
# 1. Local port forwarding
ssh -i <k> <u>@<ip> -L 8118:127.0.0.1:8118 -L 8080:127.0.0.1:8080

#2. Use Firefox + Incognito + Complete turn off/on every time for cookie-sync-extension 
- Cookie-sync-extension requires manifestv2, which currently only works with firefox. 

# 3. Load cookie-sync-extension 
- about:debugging#/runtime/this-firefox -> Load Temporary addon -> manifest.json 

# 4. When testing, complete turn off/on every time for cookie-sync-extension 
- Completely turn off/on firefox to clear cache/data 
```

## Disclaimer 

Information in this repository is for research and educational purposes. SharpSilentChrome is not intended to be used in production environments and engagements. If you are willing to do so, ensure to review the source code and modify it before using it. 
