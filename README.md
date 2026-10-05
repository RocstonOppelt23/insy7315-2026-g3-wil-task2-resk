<div align="center">

# R.E.S.K

### Proposal Workflow Management System

**From first draft to final decision, all in one place.**

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)
![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET%20Core-MVC-0C1A33)
![EF Core and Identity](https://img.shields.io/badge/EF%20Core-Identity-00B9D0)

</div>

---

## 👥 Team

**R.E.S.K Software Studio**  
**INSY7315 – Work Integrated Learning**  
**Task 2 – Group 3 – 2026**

### Group Members

- **Rocston John Oppelt** — Group Leader — `ST10276665`
- **Ethan Del Carme** — `ST10260470`
- **Kuan-Chi Fang** — `ST10041814`
- **Sayurin Naidoo** — `ST10444779`
- **Morgan Worsley** — `ST10257779`

---

## 👋 Welcome

R.E.S.K is a web-based **Proposal Workflow Management System** developed for **Cape Town TV**.

The system manages programme proposals from the initial draft through to the final decision. Producers can create and submit their programme ideas, reviewers can assess proposals, and station management can manage the overall workflow while maintaining a record of actions taken throughout the process.

The system was developed by **R.E.S.K Software Studio** as part of **INSY7315 Work Integrated Learning – Task 2, Group 3, 2026**.

The system replaces a manual proposal-management process with a centralised web application that provides structured workflows, role-based access and proposal tracking.

---

## 🎓 Task 2 Demonstration

The Task 2 demonstration presents the completed software solution and explains how it addresses the requirements identified during Task 1.

The demonstration can cover:

- Project background.
- Problem being addressed.
- System objectives.
- User roles.
- Producer functionality.
- Reviewer functionality.
- Proposal Manager functionality.
- Administrator functionality.
- Proposal workflow.
- Database functionality.
- Security features.
- Design patterns.
- Audit logging.
- Reports.
- Testing.
- Working application demonstration.

### Demonstration Video

https://youtu.be/ZFdtfRsbsn8

---

## 🎯 Project Objectives

The main objectives of the system are to:

- Digitise the programme proposal process.
- Provide a centralised platform for managing programme proposals.
- Allow producers to create and submit programme proposals.
- Allow producers to save proposals as drafts.
- Allow reviewers to assess assigned proposals.
- Allow proposal managers to manage the proposal workflow.
- Allow station management to manage proposal decisions.
- Provide role-based access to system functionality.
- Maintain a clear record of proposal activity.
- Improve the organisation and accessibility of proposal information.
- Reduce reliance on manual documentation and email communication.
- Provide a secure and maintainable software solution.

---

## ▶️ Run It

The application requires the **.NET 8 SDK**.

Download the .NET 8 SDK from:

https://dotnet.microsoft.com/download/dotnet/8.0

Open a terminal in the project folder containing `Program.cs` and run:

```powershell
dotnet run
```

Once the application starts, the terminal will display the address where the application is running.

Look for:

```text
Now listening on:
```

Open the displayed address in your web browser.

The application may normally run on an address similar to:

```text
http://localhost:5261
```

To stop the application, press:

```text
Ctrl + C
```

---

## ✨ What It Does

### For Producers

Producers can:

- Use a guided proposal form to create programme proposals.
- Save a proposal as a draft and return to it later.
- Submit proposals for review.
- View the status of submitted proposals.
- Read requested changes.
- Edit proposals when changes are requested.
- Resubmit proposals.
- Manage their own profile.
- Manage their profile picture.
- Manage their password.

---

### For Station Management

| Screen | What you can do |
| --- | --- |
| 📊 **Dashboard** | See key system information and proposal statistics. |
| 📄 **Proposals** | Assign reviewers, record reviews and confirm final decisions. |
| 👥 **Users** | Create accounts, approve registrations, and suspend or reactivate users. |
| 🗂️ **Categories** | Manage the programme categories displayed on proposal forms. |
| 🔑 **Roles & Permissions** | Control what each role can see and do. |
| 🕓 **Audit Log** | View a read-only history of system activity, including who performed an action and when. |
| 📈 **Reports** | View charts, printable summaries and CSV exports. |
| ⚙️ **System Settings** | Manage registration, workflow, languages, notifications and security settings. |

---

## 🔐 Safe by Design

Security is an important part of the Proposal Workflow Management System.

The system includes security measures such as:

- Secure user sign-in.
- Role-based access.
- A single Administrator account.
- Protection of administrator functionality.
- Lockout after repeated failed sign-in attempts.
- Automatic sign-out after a period of inactivity.
- Password rules.
- Password expiry.
- Protection of passwords from being written to the audit log.
- Input validation.
- Access control.
- HTTPS for secure communication.

These security features help protect user accounts, programme proposal information and system functionality.

---

## 🧭 A Proposal's Journey

A proposal follows a structured workflow through the system.

The main workflow is:

**Draft → In Review → Approved / Changes Requested / Rejected**

### Draft

The producer creates a programme proposal and can save it as a draft before submitting it.

### In Review

Once submitted, the proposal enters the review process and can be assigned to a reviewer.

### Approved

The proposal has successfully completed the review process and has been approved.

### Changes Requested

The reviewer or proposal manager requests changes from the producer.

The producer can make the required changes and resubmit the proposal.

### Rejected

The proposal has been declined following the review process.

---

## 👤 Roles

The system provides different roles to control access to functionality.

| Role | What they do |
| --- | --- |
| **Administrator** | Has full access to the system and manages system-level functionality. |
| **Proposal Manager** | Manages the day-to-day proposal workflow. |
| **Viewer** | Can view information but cannot make changes. |
| **Reviewer** | Reviews proposals assigned to them. |
| **Producer** | Creates, manages and submits programme proposals. |

Additional roles can be created and managed through **Roles & Permissions** where supported by the system.

Role-based access ensures that users only have access to functionality appropriate to their responsibilities.

---

## 🖥️ Front End

The front end provides the interface through which users interact with the system.

The application provides different interfaces depending on the user's role.

The front end includes functionality such as:

- Login and authentication.
- Producer workspace.
- Proposal creation.
- Proposal editing.
- Proposal submission.
- Proposal status tracking.
- Reviewer functionality.
- Management dashboards.
- User management.
- Category management.
- Reports.
- System settings.
- Audit-log viewing.

The application uses **ASP.NET Core MVC** and Razor views to organise the presentation layer.

---

## ⚙️ Back End

The back end provides the application logic and services required by the system.

The back end is responsible for:

- Processing user requests.
- Managing programme proposal information.
- Managing users and roles.
- Applying business rules.
- Managing workflow states.
- Communicating with the database.
- Maintaining audit information.
- Supporting system settings.
- Handling application functionality.

The separation between the front end, application logic and data layer helps improve maintainability and organisation.

---

## 🗄️ Database

The database provides persistent storage for information used by the application.

The system stores information such as:

- User accounts.
- User roles.
- Programme proposals.
- Proposal statuses.
- Proposal categories.
- Review information.
- System settings.
- Audit information.

The project uses **Entity Framework Core** to support communication between the application and database.

---

## 🏗️ Architecture

The system follows a layered application structure.

The overall architecture can be represented as:

```text
User
  ↓
ASP.NET Core MVC Front End
  ↓
Application / Business Logic
  ↓
Services
  ↓
Entity Framework Core
  ↓
Database
```

### Presentation Layer

The presentation layer provides the screens and interfaces users interact with.

### Application Layer

The application layer processes requests and coordinates the functionality of the system.

### Services

Services contain reusable functionality and business rules, including functionality related to roles, audit logging and system settings.

### Data Layer

The data layer manages communication with the database through Entity Framework Core.

### Database

The database provides persistent storage for application data.

This separation of responsibilities supports maintainability and makes the system easier to develop and modify.

---

## 🧩 Design Patterns

The project uses software design patterns to improve the organisation and maintainability of the application.

### Repository Pattern

The Repository Pattern separates data-access functionality from other parts of the application.

Repositories provide a structured way to perform database operations without placing all data-access logic directly inside controllers.

### Singleton Pattern

The Singleton Pattern can be used where a single shared instance of a particular resource or service is required.

### Observer Pattern

The Observer Pattern allows an object to notify other components when changes occur.

This pattern can support notification-based functionality within the application.

The use of design patterns contributes to separation of concerns, code organisation and maintainability.

---

## 🔑 Authentication and Authorisation

The system uses authentication to verify the identity of users accessing the application.

Authorisation determines what authenticated users are allowed to access.

The system uses roles to control access to functionality.

For example:

- Producers access producer functionality.
- Reviewers access assigned proposal reviews.
- Proposal Managers manage proposal workflows.
- Viewers have read-only access.
- Administrators have access to system-level functionality.

This role-based approach helps prevent users from accessing functionality outside their responsibilities.

---

## 🕓 Audit Log

The system provides an audit log that maintains a record of relevant actions performed within the application.

The audit log provides information such as:

- Who performed an action.
- What action was performed.
- When the action occurred.

The audit log is designed as a read-only record so that users cannot simply modify historical activity records.

Passwords are not written to the audit log.

---

## 📊 Reports

The system provides reporting functionality to assist station management with understanding proposal activity.

Reports can include:

- Proposal statistics.
- Charts.
- Printable summaries.
- CSV exports.

This provides management with a clearer overview of proposal activity and workflow progress.

---

## ☁️ Hosting and Deployment

The project was designed with cloud hosting in mind, using **Microsoft Azure** as the proposed cloud platform.

Azure App Service provides a managed environment for hosting web applications, including ASP.NET Core applications.

The proposed cloud architecture can include:

- Web application hosting.
- Database hosting.
- Application configuration.
- Secure HTTPS communication.

### Hosting Information

**Application hosting:**  
`[INSERT ACTUAL HOSTING SERVICE IF REQUIRED]`

**Database:**  
`[INSERT ACTUAL DATABASE TECHNOLOGY]`

**Live application:**  
`[INSERT LIVE APPLICATION URL]`

---

## 🐙 GitHub Repository

The project source code is maintained using **GitHub**.

GitHub provides version-control functionality that allows the development team to manage source-code changes and collaborate during development.

The repository contains the project source code and supporting documentation required for the Task 2 submission.

---

## 🧪 Testing

Testing was performed to verify that the system operates according to the requirements identified during the project.

Testing areas include:

### Authentication Testing

Testing that valid users can sign in and invalid authentication attempts are handled appropriately.

### Authorisation Testing

Testing that users can only access functionality associated with their assigned roles.

### Proposal Testing

Testing:

- Proposal creation.
- Draft saving.
- Proposal editing.
- Proposal submission.
- Proposal viewing.
- Proposal status tracking.

### Workflow Testing

Testing the movement of proposals through the workflow:

**Draft → In Review → Approved / Changes Requested / Rejected**

### Review Testing

Testing that reviewers can access proposals assigned to them and record review information.

### Search and Filtering Testing

Testing that users can search for and filter proposal information where functionality is provided.

### User Management Testing

Testing user creation, approval, suspension and reactivation functionality.

### Input Validation Testing

Testing that invalid or incomplete information is identified and handled correctly.

### Database Testing

Testing that information can be correctly stored and retrieved.

### Security Testing

Testing authentication, authorisation, password rules, lockout behaviour and inactivity timeouts.


---

## 🌱 Coming Next

Future versions of the system could include:

- A second sign-in step using multi-factor authentication.
- Email notifications.
- In-app notifications.
- More interface languages.
- Additional reporting features.
- Enhanced dashboard analytics.
- Additional automated testing.
- Further accessibility improvements.
- Additional cloud scalability features.

These improvements could further enhance the system and provide additional support for Cape Town TV's programme-management process.

---

## 📁 Project Structure

The project is organised into folders that separate different areas of the application.

```text
Controllers/      The pages and application actions
Models/           Data models and view models
Services/         Roles, audit log, settings and other application rules
Views/            User-interface screens
Data/             Database context and data-related functionality
wwwroot/          Static files such as CSS, JavaScript and images
Program.cs        Application configuration and startup
```

This structure helps keep the project organised and separates different areas of responsibility.

---

## 📚 Project Context

The **Cape Town TV Proposal Workflow Management System** was developed as part of the **INSY7315 Work Integrated Learning** module.

The project builds upon the requirements analysis, system design and prototype developed during Task 1.

The original problem identified was the reliance on a manual programme-proposal process involving shared documents and email communication.

The proposed system provides a centralised platform for managing proposals and allows different users to interact with the system according to their roles.

The system aims to make the proposal process more structured, traceable and manageable for Cape Town TV.

---

## 📝 Academic Integrity and AI Usage

AI Tool used was ChatGPT 5.0: OpenAI, “ChatGPT,” ChatGPT, 2025. https://chatgpt.com/ [Accessed: Sept. 28, 2026]

Reason For Use: We used Chat GPT to get more resources than what we could find on our own.

Dates Used: 28 September 2026 - 05 October 2026

Link: https://chatgpt.com/share/6ac3baae-74f0-83ea-8acb-0a5746a9056c

Action Taken: We made use of the resources provided by Chat GPT along with our knowledge and understanding of the work to help us create the project and make sure our project met the necessary standards.

---

## 📖 References

GitHub (2026) *GitHub Documentation*. Available at: https://docs.github.com/ (Accessed: 5 October 2026).

Microsoft (2026) *ASP.NET Core documentation*. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/aspnet/core/ (Accessed: 5 October 2026).

Microsoft (2026) *ASP.NET Core overview*. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/aspnet/core/overview (Accessed: 5 October 2026).

Microsoft (2026) *.NET documentation*. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/ (Accessed: 5 October 2026).

Microsoft (2026) *Azure App Service documentation*. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/azure/app-service/ (Accessed: 5 October 2026).

Microsoft (2026) *Entity Framework Core documentation*. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/ef/core/ (Accessed: 5 October 2026).

Microsoft (2026) *ASP.NET Core Identity documentation*. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity (Accessed: 5 October 2026).

