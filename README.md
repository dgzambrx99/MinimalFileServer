# MinimalFileServer
Application Overview
Purpose: A lightweight web-based file server for browsing, uploading, downloading, and previewing files and directories.

Core Technologies:

Backend: ASP.NET Core
Frontend: HTML, Bootstrap (for styling and responsiveness)
APIs: RESTful endpoints for file operations
Features:

File Browsing:

View directories and files in a structured layout.
Navigate through subdirectories.
File Upload:

Upload multiple files using an input form.
Immediate refresh of file list after upload.
File Download:

Direct links for downloading files.
Works with all file types.
File Preview:

In-browser previews for specific file types:
Images (.jpg, .jpeg, .png, .gif).
Text files (.txt, .log).
A Bootstrap modal is used for displaying previews.
Search:

Filter files and directories by name.
Provides instant search results from the API.
Directory Navigation:

Clickable links to navigate into directories.
Displays files and subdirectories within the selected directory.
Technical Details
Backend (ASP.NET Core)
Structure:

RESTful API endpoints for file operations:
/api/files/{*path}: Fetch directory contents.
/api/files/upload: Handle file uploads.
/api/files/download/{*filePath}: Serve file downloads.
/api/files/search: Search for files.
File Management:

Uses System.IO for interacting with the file system.
Supports directory creation and safe file uploads.
Authentication:

Includes basic authentication middleware.
Credentials can be stored in configuration for secure access.
Frontend (HTML + Bootstrap)
Design:

Responsive layout using Bootstrap 5.
Clean and user-friendly interface with:
Navbar for branding.
File list as a styled list-group.
Buttons for upload, preview, and search.
Dynamic Features:

JavaScript handles API calls and updates the DOM dynamically.
Previews are shown using a Bootstrap modal.
User Workflow
Browse Files:

Open the web application.
View files and directories in the root folder.
Upload Files:

Use the upload form to add files.
Uploaded files appear immediately in the list.
Download Files:

Click on file links to download them directly.
Preview Files:

Click "Preview" for supported file types to view them in the modal.
Search:

Enter a file name in the search bar to filter the file list.
Navigate Directories:

Click on directory names to browse their contents.
Strengths
Ease of Use:

Intuitive interface for non-technical users.
Supports basic file operations with minimal setup.
Lightweight:

No database dependency.
Suitable for small-scale file management.
Extensible:

Can be enhanced with additional features like:
Drag-and-drop uploads.
Advanced file previews (e.g., PDF or video support).
User authentication and role-based access control.
Potential Improvements
Pagination or Lazy Loading:

Optimize performance for directories with many files.
Advanced Authentication:

Replace basic authentication with OAuth2 or JWT for security.
Error Handling:

Improve user feedback for failed uploads or file not found errors.
Enhanced Previews:

Add support for PDF, video, and audio previews.
File Operations:

Add features like file renaming, deletion, and folder creation.
Meta Purpose
This app demonstrates a practical and straightforward implementation of a file server with modern web technologies, balancing simplicity with functionality. It can be used as:

A learning project for ASP.NET Core and Bootstrap.
A foundation for enterprise-grade file management with more advanced features.
A lightweight solution for personal or small business file sharing.

Copyright (C) Astro Aluminum 2024. All Rights Reserved.

For internal use only. Do not redistribute.

Copyright laws and international treaties protect this app. Unauthorized redistribution of this app without express, written permission from our legal department may entail severe civil or criminal penalties.
