# Cape Town TV Programme Proposal Management System

## INSY7315 – Work Integrated Learning

### Task 2 – Software Development Project

---

## 1. Project Overview

The **Cape Town TV Programme Proposal Management System** is a web-based application developed for Cape Town TV by **R.E.S.K Software Studio – Group 3**.

The system was designed to improve and digitise the existing programme proposal process, which previously relied on manual documentation, shared documents and email communication.

The proposed system provides a centralised platform where producers can create and submit programme proposals, while reviewers and proposal managers can access, review and manage submitted proposals.

The main proposal workflow is:

**Submitted → Under Review → Approved / Declined**

The system was developed based on the requirements, system design and prototype established during Task 1.

---

## 2. Project Objectives

The main objectives of the Cape Town TV Programme Proposal Management System are to:

* Digitise the programme proposal process.
* Provide a centralised platform for managing programme proposals.
* Allow producers to create and submit programme proposals.
* Allow reviewers and proposal managers to review submitted proposals.
* Provide role-based access to system functionality.
* Improve the organisation and accessibility of proposal information.
* Reduce reliance on manual documentation and email communication.
* Provide a secure and maintainable software solution.
* Support cloud-based deployment and future scalability.

---

## 3. System Features

The system provides functionality for the different users involved in the programme proposal process.

### 3.1 Producer

Producers can:

* Register and authenticate with the system.
* Access the producer portal.
* Create programme proposals.
* Submit programme proposals for review.
* View submitted proposals.
* Edit proposals where permitted.
* Track the status of their proposals.

### 3.2 Reviewer / Proposal Manager

Reviewers and proposal managers can:

* Authenticate with the system.
* Access submitted proposals.
* Search and filter proposals.
* Review proposal information.
* Change proposal statuses.
* Approve or decline proposals where appropriate.

### 3.3 Administrator

Administrators are responsible for managing the system and its users according to their assigned permissions.

Role-based access ensures that users have access to functionality appropriate to their role.

---

## 4. Proposal Workflow

The system follows a structured proposal-management workflow.

### Step 1 – Proposal Submission

A producer creates a programme proposal and submits it through the system.

### Step 2 – Review

The submitted proposal becomes available to the reviewer or proposal manager for assessment.

### Step 3 – Decision

After reviewing the proposal, the reviewer or proposal manager can approve or decline the proposal.

### Proposal Statuses

The main proposal statuses are:

* **Submitted**
* **Under Review**
* **Approved**
* **Declined**

This workflow provides users with a clear indication of the current stage of each programme proposal.

---

## 5. Front End

The front end provides the user interface through which users interact with the system.

The interface is designed around the three main user roles:

* Producer
* Reviewer / Proposal Manager
* Administrator

The front end provides functionality such as:

* User authentication.
* Navigation.
* Dashboards.
* Proposal forms.
* Proposal viewing.
* Proposal submission.
* Proposal management.
* Searching and filtering.

The application follows the **Model-View-Controller (MVC)** approach to organise the presentation layer.

ASP.NET Core supports MVC-based web applications and provides functionality such as dependency injection, configuration, middleware and security features.

---

## 6. Back End

The back end provides the application logic and services required by the system.

The back end is responsible for:

* Processing requests from the front end.
* Managing programme proposal information.
* Communicating with the database.
* Applying business rules.
* Managing user-related functionality.
* Supporting API functionality.
* Handling data operations.

The back-end architecture separates application logic from the presentation layer, helping improve maintainability and organisation.

ASP.NET Core provides a framework for developing web applications and HTTP APIs, making it suitable for implementing the service layer of the system.

---

## 7. Database

The database provides persistent storage for information used by the application.

The system requires storage for information such as:

* User accounts.
* User roles.
* Programme proposals.
* Proposal statuses.
* Proposal information.
* Review information.
* Other data required by the application.

The database forms part of the application's data layer and is accessed through the back-end services.

---

## 8. Architecture

The system follows a layered architecture that separates the major responsibilities of the application.

The overall structure can be represented as:

```text
User
  ↓
Web Front End
  ↓
Application / API Layer
  ↓
Business Logic
  ↓
Data Access Layer
  ↓
Database
```

### Presentation Layer

The presentation layer provides the interface through which users interact with the system.

### Application / API Layer

The API layer handles requests from the front end and provides access to application functionality.

### Business Logic Layer

The business logic layer processes application rules and manages the behaviour of the system.

### Data Access Layer

The data access layer handles communication with the database.

### Database Layer

The database stores the information required by the system.

This separation of responsibilities supports maintainability and allows individual components to be developed and maintained independently.

---

## 9. Design Patterns

The project makes use of recognised software design patterns to improve the organisation and maintainability of the application.

### 9.1 Repository Pattern

The **Repository Pattern** separates data-access functionality from the rest of the application.

Repositories are responsible for performing data operations while keeping database-related functionality separate from other application components.

This improves separation of concerns and makes data-access operations easier to manage.

### 9.2 Singleton Pattern

The **Singleton Pattern** is used where a single shared instance of a particular service or resource is required.

This pattern prevents unnecessary creation of multiple instances of the same resource.

### 9.3 Observer Pattern

The **Observer Pattern** allows an object to notify other components when a change occurs.

This can be used to create a notification mechanism where interested components can respond to changes in the system.

The use of these design patterns supports a more structured and maintainable software architecture.

---

## 10. Security

Security is an important consideration because the system manages user accounts and programme proposal information.

Security considerations include:

* User authentication.
* Role-based authorisation.
* Input validation.
* Input sanitisation.
* HTTPS.
* Secure password handling.
* Access control.
* Audit logging.

### Authentication

Authentication ensures that users must identify themselves before accessing protected functionality.

### Authorisation

Role-based authorisation ensures that users can only access functionality appropriate to their assigned role.

For example, producer functionality differs from reviewer or administrator functionality.

### Input Validation

Input validation helps prevent invalid or unexpected information from being submitted to the application.

### Secure Communication

HTTPS is used to protect communication between users and the application.

### Password Security

Passwords should be securely handled and should not be stored as plain text.

These security considerations help protect user accounts, programme proposals and other system information.

---

## 11. Hosting and Deployment

The project was designed with cloud hosting in mind, using **Microsoft Azure** as the proposed cloud platform.

Azure App Service provides a managed platform for hosting web applications, including ASP.NET and ASP.NET Core applications.

The proposed cloud architecture consists of components such as:

* Web application hosting.
* API/back-end hosting.
* Database hosting.
* Application configuration.
* Secure HTTPS communication.

Azure was selected in the project design because cloud hosting can provide scalability and support the future growth of the application.

---

## 12. GitHub Repository

The project source code is maintained using **GitHub**.

GitHub provides version control functionality that allows the development team to manage source-code changes and collaborate on the project.

The repository contains the project's source code and supporting documentation required for the Task 2 submission.

---

## 13. Testing

Testing was performed to verify that the system functions according to the requirements identified during the project.

Testing considerations include:

### Authentication Testing

Testing that users can authenticate correctly and that invalid authentication attempts are handled appropriately.

### Authorisation Testing

Testing that users can only access functionality associated with their assigned roles.

### Proposal Testing

Testing the creation, editing, viewing and submission of programme proposals.

### Workflow Testing

Testing that proposals can progress through the expected workflow:

**Submitted → Under Review → Approved / Declined**

### Search and Filtering Testing

Testing that users can search for and filter proposals according to the available criteria.

### Input Validation Testing

Testing that invalid or incomplete information is identified and handled correctly.

### Database Testing

Testing that application data can be correctly stored and retrieved from the database.

### API Testing

Testing communication between the front end and back-end/API components.

Testing provides evidence that the implemented system performs the required functions and helps identify errors that need to be corrected during development.

---

## 14. Future Improvements

Although the system addresses the core programme proposal workflow, additional functionality could be implemented in future versions.

Potential improvements include:

* Email notifications when proposal statuses change.
* Advanced proposal searching and filtering.
* Detailed proposal audit history.
* Improved administrator controls.
* Additional reporting functionality.
* Dashboard analytics.
* Automated proposal notifications.
* Enhanced accessibility.
* Additional automated testing.
* Improved cloud scalability.
* Mobile-friendly enhancements.
* Additional security features.

These improvements could further support Cape Town TV's programme-management processes.

---

## 15. Task 2 Demonstration / Presentation

The Task 2 presentation demonstrates the completed software solution and explains how the system addresses the requirements identified during Task 1.

The demonstration covers areas such as:

* Project background.
* Problem being addressed.
* System objectives.
* User roles.
* Front-end functionality.
* Back-end functionality.
* Database functionality.
* Proposal workflow.
* Security features.
* Design patterns.
* Cloud hosting.
* GitHub repository.
* Testing.
* Demonstration of the working application.

### Demonstration Video

`[Will Add Link When Video Is Done]`

---

## 16. Team Members

### R.E.S.K Software Studio – Group 3

| Team Member         | Student Number | Role             |
| ------------------- | -------------- | ---------------- |
| Rocston John Oppelt | ST10276665     | Project Manager  |
| Ethan Del Carme     | ST10260470     | Development Team |
| Kuan-Chi Fang       | ST10041814     | Development Team |
| Sayurin Naidoo      | ST10444779     | Development Team |
| Morgan Worsley      | ST10257779     | Development Team |

---

## 17. Project Context

The **Cape Town TV Programme Proposal Management System** was developed as part of the **INSY7315 Work Integrated Learning** module.

The project builds on the requirements analysis, system design and prototype developed during Task 1.

The original problem identified during Task 1 was the reliance on a manual programme-proposal process involving shared documents and email communication.

The proposed software solution provides a centralised system for managing programme proposals and allows the different users involved in the process to interact with the system according to their roles.

Task 2 focuses on translating the requirements and prototype developed in Task 1 into a functional software solution.

---

## 18. AI Declaration

AI Tool used was ChatGPT 5.0: 
OpenAI, “ChatGPT,” ChatGPT, 2025. https://chatgpt.com/  [Accessed: Sept. 28, 2026]

Reason For Use: We used Chat GPT to get more resources than what we could find on our own and also give us ideas on the project and what to implement and focus on.  

Dates Used: 25 September 2026 - 05 October 2026

Link: https://chatgpt.com/share/6abb76d2-fa10-83ea-963a-eb366c8bcbf9

Action Taken: We made use of the resources and ideas provided by Chat GPT along with our knowledge and understanding of the work to help us create the project and make sure our project met the necessary standards.


---

# 19. Reference List

GitHub (2026) *GitHub documentation*. Available at: https://docs.github.com/ (Accessed: 29 September 2026).

Microsoft (2026) *ASP.NET Core documentation*. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/aspnet/core/ (Accessed: 29 September 2026).

Microsoft (2026) *ASP.NET Core overview*. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/aspnet/core/overview (Accessed: 29 September 2026).

Microsoft (2026) *.NET documentation*. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/dotnet/ (Accessed: 29 September 2026).

Microsoft (2026) *Azure App Service documentation*. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/azure/app-service/ (Accessed: 29 September 2026).

Microsoft (2026) *Azure documentation*. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/azure/ (Accessed: 29 September 2026).




