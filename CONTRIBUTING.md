# How to Contribute #

We're always looking for people to help make Kavita even better, there are a number of ways to contribute.

## Documentation ##
Setup guides, FAQ, the more information we have on the [wiki](https://wiki.kavitareader.com/) the better.

## Development ##

### Tools required ###
- Visual Studio 2019 or higher (https://www.visualstudio.com/vs/).  The community version is free and works fine. [Download it here](https://www.visualstudio.com/downloads/).
- Rider (optional to Visual Studio) (https://www.jetbrains.com/rider/)  
- HTML/Javascript editor of choice (VS Code/Sublime Text/Webstorm/Atom/etc)
- [Git](https://git-scm.com/downloads)
- [NodeJS](https://nodejs.org/en/download/) (Node 14.X.X or higher)
- .NET 5.0+ 

### Getting started ###

1. Fork Kavita
2. Clone the repository into your development machine. [*info*](https://docs.github.com/en/github/creating-cloning-and-archiving-repositories/cloning-a-repository-from-github)
3. Install the required Node Packages
    - cd Kavita/UI/Web
    - `npm install`
    - `npm install -g @angular/cli`
4. Start angular server `ng serve`
5. Build the project in Visual Studio/Rider, Setting startup project to `API`
6. Debug the project in Visual Studio/Rider
7. Open http://localhost:4200
8. (Deployment only) Run build.sh and pass the Runtime Identifier for your OS or just build.sh for all supported RIDs.


### Contributing Code ###
- If you're adding a new, already requested feature, please comment on [Github Issues](https://github.com/Kareadita/Kavita/issues "Github Issues") so work is not duplicated (If you want to add something not already on there, please talk to us first)
- Rebase from Kavita's develop branch, don't merge
- Make meaningful commits, or squash them
- Feel free to make a pull request before work is complete, this will let us see where its at and make comments/suggest improvements
- Reach out to us on the discord if you have any questions
- Add tests (unit/integration)
- Commit with *nix line endings for consistency (We checkout Windows and commit *nix)
- One feature/bug fix per pull request to keep things clean and easy to understand
- Use 4 spaces instead of tabs, this is the default for VS 2019 and WebStorm (to my knowledge)
    - Use 2 spaces for UI files

### Pull Requesting ###
- Only make pull requests to develop, never main, if you make a PR to main we'll comment on it and close it
- You're probably going to get some comments or questions from us, they will be to ensure consistency and maintainability
- We'll try to respond to pull requests as soon as possible, if its been a day or two, please reach out to us, we may have missed it
- Each PR should come from its own [feature branch](http://martinfowler.com/bliki/FeatureBranch.html) not develop in your fork, it should have a meaningful branch name (what is being added/fixed)
    - new-feature (Good)
    - fix-bug (Good)
    - patch (Bad)
    - develop (Bad)
    - feature/parser-enhancements (Great)
    - bugfix/book-issues (Great)

If you have any questions about any of this, please let us know.
