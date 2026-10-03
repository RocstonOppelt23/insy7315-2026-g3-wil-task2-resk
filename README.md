<div align="center">

# R.E.S.K

### Proposal Workflow Management System

**From first draft to final decision, all in one place.**

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET%20Core-MVC-0C1A33)
![EF Core and Identity](https://img.shields.io/badge/EF%20Core-Identity-00B9D0)

</div>

---

## 👋 Welcome

R.E.S.K is a web system for **Cape Town TV** that looks after programme proposals from start to finish.

Producers send in their ideas, reviewers assess them, and station management decides, with a clear record of every step along the way.

Built with care by **R.E.S.K Software Studio** for INSY7315 Work Integrated Learning (Task 2, Group 3, 2026).

## ▶️ Run it

All you need is the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

Open a terminal in the project folder (the one that contains `Program.cs`) and run:

~~~powershell
dotnet run
~~~

Then open the address shown next to `Now listening on:` in your browser. It is normally <http://localhost:5261>.

To stop the site, press `Ctrl + C` in the terminal.

## ✨ What it does

### For producers

- A guided proposal form that walks you through each step
- Save a draft at any time and come back to it later
- See the status of every proposal you have submitted
- Read requested changes, edit your proposal and send it again
- Your own profile, picture and password

### For station management

| Screen | What you can do |
| --- | --- |
| 📊 **Dashboard** | See the key numbers and a chart of proposals over time. |
| 📄 **Proposals** | Assign a reviewer, record the review and confirm the final decision. |
| 👥 **Users** | Create accounts, approve new registrations, and suspend or reactivate users. |
| 🗂️ **Categories** | Manage the programme categories shown on the proposal form. |
| 🔑 **Roles & permissions** | Decide exactly what each role can see and do. |
| 🕓 **Audit log** | A read-only history of who did what, and when. |
| 📈 **Reports** | Charts, a printable summary and CSV exports. |
| ⚙️ **System settings** | Registration, workflow, languages, notifications and security. |

### Safe by design

- Secure sign-in with a role for every user
- One Administrator account, which cannot be edited by anyone else
- Lockout after repeated failed sign-ins
- Automatic sign-out after a period of inactivity
- Password rules and password expiry
- Passwords are never written to the audit log

## 🧭 A proposal's journey

**Draft** → **In review** → **Approved**, **Changes requested** or **Rejected**

## 👤 Roles

| Role | What they do |
| --- | --- |
| **Administrator** | Full access. One account only. |
| **Proposal Manager** | Runs the day-to-day proposal workflow. |
| **Viewer** | Can look, but not change. |
| **Reviewer** | Reviews the proposals assigned to them. |
| **Producer** | Creates and submits proposals. |

More roles can be created at any time in **Roles & permissions**.

## 🛠️ Built with

- **ASP.NET Core MVC** on .NET 8 (C#)
- **Razor views** with plain HTML, CSS and JavaScript
- **Entity Framework Core** and **ASP.NET Core Identity**

## 📁 Project structure

~~~text
Controllers/      the pages and what they do
Models/           the data and view models
Services/         roles, audit log, settings and other rules
Views/            the screens, one folder each
Data/             the database context
wwwroot/          static files
Program.cs        where the site starts
~~~

## 🌱 Coming next

- A second sign-in step (multi-factor authentication)
- Email and in-app notifications
- More interface languages

## 🎓 Team

**R.E.S.K Software Studio** - INSY7315, Group 3

| Student number | Contribution |
| --- | --- |
| ST10257779 | Front-end: producer workspace and admin area |

---

<div align="center">

Developed for INSY7315 Work Integrated Learning (2026). For academic use.

</div>
