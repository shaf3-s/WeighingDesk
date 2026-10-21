# WeighingDesk

A Windows desktop application for managing vehicle weigh-ins. Built with Windows Forms and .NET 8, it stores weighing records locally and supports RS-232 scale communication.

## Requirements

- Windows 10 or later.
- Visual Studio 2022 with the .NET desktop development workload, or the .NET 8 SDK.
- A compatible weighing scale and serial cable for testing live scale communication.

## Open and run the project

Open `WeighingDesk.slnx` in Visual Studio, allow NuGet packages to restore, and run the project. Use **Demo Mode** to explore the screens and weighing calculations without a scale.

## Features

- Vehicle tickets with first and second weigh-ins and automatic net-weight calculation.
- A local SQLite database and per-user application settings.
- RS-232 serial port settings and support for Tunaylar Load Line 2 indicator data.
- Search, reports, ticket printing, and Excel export.
- Arabic, English, and Kurdish interface options, with ticket template and print settings.

## Data storage

The database and connection settings are stored in the current user's local application data folder under `%LOCALAPPDATA%\WeighingDesk`.

## Display layout note

The application layout may change depending on the computer's screen resolution and display scaling. Some sections or controls may appear at different sizes or positions on another computer.

## Scale communication note

Serial protocol settings depend on the scale indicator model and its port configuration. Verify the COM port, baud rate, parity, data bits, stop bits, and communication mode on the target computer before using the application for live weighing. Demo Mode does not connect to a scale or provide real readings.

