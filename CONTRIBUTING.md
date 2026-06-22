# How to Contribute #

We're always looking for people to help make Kavita even better, there are a number of ways to contribute.

## Contribute to the Wiki ##

Our documentation is a community effort! You can fix a typo, clarify a setup step, or add a new FAQ to the wiki. The more information we have on the [wiki](https://wiki.kavitareader.com/contributing) the better.

The docs are maintained in the [Wiki-Nextra](https://github.com/Kareadita/Wiki-Nextra) repository.

## Getting Started: Development Setup ##

### Tools required ###
- .NET editor
  - [Rider](https://www.jetbrains.com/rider/) (preferred)   
  - [Visual Studio](https://www.visualstudio.com/downloads/) 2019 or higher
- HTML/Javascript editor (VS Code/Sublime Text/Webstorm/Atom/etc)
- [Git](https://git-scm.com/downloads)
- [NodeJS](https://nodejs.org/en/download/) (Node 18.13.X or higher)
- .NET 9.0+
- dotnet tool install -g Swashbuckle.AspNetCore.Cli
- dotnet cli tools [link](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) 

### Fork and Clone Kavita

Fork the repository on GitHub, then clone your fork onto your local development machine.

### Frontend

1. Install the required Node Packages
    - `cd Kavita/UI/Web`
    - `npm install`
    - `npm install -g @angular/cli`
2. Start the frontend 
    - `npm run start`
3. Open http://localhost:4200

#### Debugging on Device
  Run `npm run start-proxy` instead to have the Angular application proxy the requests to the backend.

### Backend

1. Build the project in Visual Studio/Rider, and set the startup project to `Kavita.Server (Server)`
2. Debug the project in Visual Studio/Rider

#### Troubleshooting for Apple users

The backend may fail to start due to port 5000 already being in use. To fix this, **temporarily turn off AirPlay Receiver in System Settings.**

You can re-enable the setting later, and it will bind to a different port. You may need to do this again after an update or reboot.

### Deployment
Run build.sh and pass the Runtime Identifier for your OS or just build.sh for all supported RIDs.

### Database Changes
- When you need to make changes to the Db, update the Entities then run:
`dotnet ef migrations add --project Kavita.Database\Kavita.Database.csproj --startup-project Kavita.Server\Kavita.Server.csproj --context Kavita.Database.DataContext --configuration Debug --output-dir Migrations MeaningfulName`

## Contributing Code

### General Guidelines
- If you're working on a requested feature, please comment on the [Github Issue](https://github.com/Kareadita/Kavita/issues "Github Issues") so work is not duplicated
- If you want to add something without an existing issue, please talk to us first or open an issue
- Rebase from Kavita's `develop` branch, don't merge
- Make meaningful commits, or squash them
- Add tests (unit/integration)
- Reach out to us on Discord if you have any questions

### Formatting
- Commit with *nix line endings for consistency (We checkout Windows and commit *nix)
- Use 4 spaces instead of tabs, this is the default for VS 2019 and WebStorm (to our knowledge)
- Use 2 spaces for UI files

### Pull Requests

Feel free to make a pull request before work is complete, this will let us see where it's at and make comments/suggest improvements.

#### 1. Use feature branches to develop
Each PR should come from its own [feature branch](http://martinfowler.com/bliki/FeatureBranch.html) (not `develop` in your fork). It should have a meaningful branch name to describe what is being added/fixed.

| Great Example | Bad Example |
| - | - |
| `feature/parser-enhancements` | `new-feature`  |
| `bugfix/sidenav-mobile-overlap` | `fix-bug` |
| `docs/contributing` | `contributiondocs` |

#### 2. Don't submit large pull requests
Make sure there is only one feature/bug fix per pull request to keep things clean and easy to understand.

#### 3. Only make pull requests to `develop`
If you make a PR to `main` we'll comment on it and close it.

#### 4. Review our comments or questions
We review each PR for consistency and maintainability.
We'll try to respond as soon as possible. If it's been a day or two, please reach out to us, we may have missed it.

## API Reference (Swagger)
 To view the full API documentation and test endpoints locally, run the server in Debug mode.

1. **Navigate to server directory:** `cd Kavita/Kavita.Server`
2. **Start server:** `dotnet run -c Debug`        
3. **Access the UI at:** http://localhost:5000/swagger/index.html

For any build issues run
` swagger tofile --output ../openapi.json API/bin/Debug/net8.0/API.dll v1` to see the error and correct it.

## Building external scripts/apps
We welcome anyone to build external scripts and applications. Reach out to us about publishing, we will link it from our wiki and Discord. 

**Please do not use words like "Kavita reader" or "Kavita" as your explicit app name.** Use of "[name]: A Kavita Reader" is preferred. 

If you have any questions about any of this, please let us know.


## Misc
- Localization should use component name for primary grouping, -label for labels, -alt for accessibility text
- /theme can be used to visualize different components available for building
