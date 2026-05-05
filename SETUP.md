# JSBAI .NET MVC Backend — Setup Guide

## Architecture

```
Frontend (GitHub Pages — free)
    index.html + admin.html
         ↓ HTTP calls to
.NET 8 MVC Backend (Railway.app — free)
    Controllers → Services → Database
         ↓ saves to
SQLite Database (file inside the app)
         ↓ sends via
Gmail SMTP (emails)
```

---

## Step 1 — Get Gmail App Password (for emails)

Normal Gmail password won't work for sending emails via code.
You need a special "App Password".

1. Go to myaccount.google.com
2. Click "Security" in the left menu
3. Make sure "2-Step Verification" is ON (turn it on if not)
4. Go back to Security → scroll down → click "App passwords"
5. Under "Select app" choose "Mail"
6. Under "Select device" choose "Other" → type "JSBAI"
7. Click "Generate"
8. Copy the 16-character password shown (looks like: abcd efgh ijkl mnop)
   Remove the spaces — you need: abcdefghijklmnop

---

## Step 2 — Fill in appsettings.json

Open appsettings.json and fill in:

```json
{
  "AdminPassword": "choose-any-password",
  "Email": {
    "SenderEmail": "your.actual@gmail.com",
    "AppPassword": "your16charapppassword",
    "EditorEmail": "your.actual@gmail.com"
  }
}
```

Also open Program.cs and update the CORS URL:
Find this line:
    .WithOrigins("https://khanharsh193-ai.github.io",
Replace khanharsh193-ai with YOUR actual GitHub username.

---

## Step 3 — Deploy to Railway (the server)

Railway is a free hosting platform. It reads your code from GitHub and runs it automatically.

1. Go to railway.app → Sign Up with GitHub
2. Click "New Project"
3. Click "Deploy from GitHub repo"
4. Select your repository
   (you'll need to push the .NET files to GitHub first — see Step 3b)
5. Railway detects the Dockerfile automatically and starts building
6. Wait ~3 minutes for it to build and deploy
7. Click on your project → Settings → Networking → Generate Domain
8. Copy the URL — looks like: https://jsbai-backend-production.up.railway.app

### Step 3b — Push .NET files to GitHub

Create a SEPARATE repository for the backend (e.g. "jsbai-backend").
Upload all files EXCEPT index.html and admin.html to it:
- Controllers/
- Models/
- Data/
- Services/
- DTOs/
- Program.cs
- JsbaiBackend.csproj
- appsettings.json
- Dockerfile

---

## Step 4 — Connect Frontend to Backend

In your jsbai-journal repository (the frontend):
Open index.html → find:
    const API_URL = 'YOUR_RAILWAY_BACKEND_URL';
Replace with your Railway URL.

Open admin.html → same change:
    const API_URL = 'YOUR_RAILWAY_BACKEND_URL';
    const ADMIN_PASSWORD = 'same-password-as-appsettings';

Commit both files.

---

## MVC Structure Explained

| Layer | File | What it does |
|---|---|---|
| Model | Models/Submission.cs | Defines what a submission looks like |
| Controller | Controllers/SubmissionsController.cs | Handles POST /api/submissions |
| Controller | Controllers/AdminController.cs | Handles GET/PATCH /api/admin/* |
| Service | Services/EmailService.cs | Sends emails via Gmail |
| Service | Services/FileService.cs | Saves uploaded files to disk |
| Database | Data/AppDbContext.cs | Connects C# to SQLite |
| Entry point | Program.cs | Starts everything, registers services |

---

## API Endpoints

| Method | URL | What it does |
|---|---|---|
| GET | /api/submissions/health | Check if API is running |
| POST | /api/submissions | Submit a manuscript |
| GET | /api/admin/submissions | Get all submissions (requires password header) |
| GET | /api/admin/submissions/{refId} | Get one submission |
| PATCH | /api/admin/submissions/status | Update status |
| PATCH | /api/admin/submissions/notes | Save editor notes |
| GET | /api/admin/stats | Get dashboard counts |
| GET | /swagger | Auto-generated API documentation |

---

## Testing the API

After deploying, go to:
    https://your-railway-url.up.railway.app/swagger

This shows all your API endpoints in a visual interface.
You can test each one directly from the browser — no code needed.
