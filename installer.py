#!/usr/bin/env python3

import os
import platform
import shutil
import subprocess
import threading
import tkinter as tk
from tkinter import ttk
from tkinter import filedialog, messagebox
from pathlib import Path

BASE = Path.home() / ".local" / "share" / "OSFR-Linux"
DEFAULT = BASE

BG = "#080d18"
PANEL = "#111a2b"
PANEL2 = "#18243a"
BLUE = "#368cff"
BLUE_HOVER = "#55a5ff"
GREEN = "#45d483"
RED = "#e05252"
GOLD = "#e3b95f"
WHITE = "#f5f7fb"
MUTED = "#91a0b8"


class Installer(tk.Tk):

    def __init__(self):
        super().__init__()

        self.title("Open Source Free Realms — Linux Installer")
        self.geometry("1050x720")
        self.minsize(900, 620)
        self.configure(bg=BG)

        self.install_dir = tk.StringVar(value=str(DEFAULT))
        self.busy = False
        self.display_progress = 0.0
        self.target_progress = 0.0
        self.last_step = 0

        self.protocol("WM_DELETE_WINDOW", self.destroy)

        self.create_ui()
        self.refresh()

    def text(self, parent, value, size=10, bold=False, color=WHITE):
        return tk.Label(
            parent,
            text=value,
            bg=parent["bg"],
            fg=color,
            font=("DejaVu Sans", size, "bold" if bold else "normal")
        )

    def create_ui(self):

        # MAIN PANEL
        panel = tk.Frame(self, bg=PANEL)
        panel.pack(fill="both", expand=True, padx=42, pady=12)

        # HERO
        hero = tk.Frame(panel, bg=PANEL2, height=145)
        hero.pack(fill="x", padx=22, pady=22)
        hero.pack_propagate(False)

        left = tk.Frame(hero, bg=PANEL2)
        left.pack(side="left", fill="both", expand=True, padx=25, pady=20)

        self.text(
            left,
            "WELCOME BACK TO FREE REALMS",
            20,
            True
        ).pack(anchor="w")

        self.text(
            left,
            "Proton-powered Open Source Free Realms for Linux",
            10,
            False,
            MUTED
        ).pack(anchor="w", pady=(7, 0))

        self.hero = self.text(
            left,
            "",
            10,
            True,
            GREEN
        )
        self.hero.pack(anchor="w", pady=(12, 0))

        # SYSTEM CHECK
        self.text(
            panel,
            "SYSTEM REQUIREMENTS",
            11,
            True,
            MUTED
        ).pack(anchor="w", padx=27)

        checks = tk.Frame(panel, bg=PANEL)
        checks.pack(fill="x", padx=23, pady=(7, 14))

        self.checks = {}

        entries = [
            ("linux", "Linux operating system"),
            ("cpu", "x86_64 processor"),
            ("steam", "Steam installation"),
            ("proton", "Steam Proton"),
        ]

        for i, (key, name) in enumerate(entries):

            box = tk.Frame(checks, bg=PANEL2, height=48)
            box.grid(
                row=i // 2,
                column=i % 2,
                sticky="ew",
                padx=4,
                pady=4
            )
            box.grid_propagate(False)

            self.text(
                box,
                name,
                10,
                True
            ).pack(side="left", padx=14)

            status = self.text(
                box,
                "CHECKING",
                9,
                True,
                MUTED
            )
            status.pack(side="right", padx=14)

            self.checks[key] = status

        checks.columnconfigure(0, weight=1)
        checks.columnconfigure(1, weight=1)

        # LOCATION
        self.text(
            panel,
            "INSTALLATION LOCATION",
            11,
            True,
            MUTED
        ).pack(anchor="w", padx=27, pady=(5, 5))

        row = tk.Frame(panel, bg=PANEL)
        row.pack(fill="x", padx=27)

        tk.Entry(
            row,
            textvariable=self.install_dir,
            bg="#080e1b",
            fg=WHITE,
            insertbackground=WHITE,
            relief="flat",
            font=("DejaVu Sans", 10)
        ).pack(
            side="left",
            fill="x",
            expand=True,
            ipady=10
        )

        tk.Button(
            row,
            text="BROWSE",
            command=self.browse,
            bg="#293852",
            fg=WHITE,
            activebackground="#3b4c6b",
            activeforeground=WHITE,
            relief="flat",
            font=("DejaVu Sans", 9, "bold"),
            padx=18,
            pady=10
        ).pack(side="left", padx=(8, 0))

        # STATUS
        self.status = self.text(
            panel,
            "",
            10,
            False,
            MUTED
        )
        self.status.pack(anchor="w", padx=27, pady=(12, 0))

        # INSTALLATION PROGRESS
        self.progress = ttk.Progressbar(
            panel,
            orient="horizontal",
            mode="determinate",
            maximum=8,
            value=0
        )
        self.progress.pack(
            fill="x",
            padx=27,
            pady=(10, 0)
        )

        self.progress_text = self.text(
            panel,
            "Ready",
            9,
            False,
            MUTED
        )
        self.progress_text.pack(
            anchor="w",
            padx=27,
            pady=(4, 0)
        )

        # BOTTOM BUTTONS
        bottom = tk.Frame(panel, bg=PANEL)
        bottom.pack(side="bottom", fill="x", padx=27, pady=27)

        self.action = tk.Button(
            bottom,
            text="INSTALL",
            command=self.action_clicked,
            bg=BLUE,
            fg=WHITE,
            activebackground=BLUE_HOVER,
            activeforeground=WHITE,
            relief="flat",
            font=("DejaVu Sans", 12, "bold"),
            padx=40,
            pady=13
        )
        self.action.pack(side="left")

        tk.Button(
            bottom,
            text="CLOSE INSTALLER",
            command=self.destroy,
            bg="#293852",
            fg=WHITE,
            activebackground="#3b4c6b",
            activeforeground=WHITE,
            relief="flat",
            font=("DejaVu Sans", 10, "bold"),
            padx=25,
            pady=13
        ).pack(side="right")

    def steam(self):

        candidates = [
            Path.home() / ".steam" / "debian-installation",
            Path.home() / ".local" / "share" / "Steam",
            Path.home() / ".steam" / "steam",
        ]

        for path in candidates:
            if path.exists():
                return path

        return None

    def proton(self):

        steam = self.steam()

        if not steam:
            return None

        common = steam / "steamapps" / "common"

        if not common.exists():
            return None

        for name in [
            "Proton - Experimental",
            "Proton Hotfix",
        ]:
            p = common / name / "proton"

            if p.is_file():
                return p

        try:
            for directory in common.iterdir():

                p = directory / "proton"

                if p.is_file() and "Proton" in directory.name:
                    return p

        except Exception:
            pass

        return None

    def installed(self):

        root = Path(self.install_dir.get()).expanduser()

        possible = [
            root / "OSFRLauncher",
            root / "Launcher" / "OSFRLauncher",
            root / "bin" / "OSFRLauncher",
            root / "Client",
        ]

        return any(x.exists() for x in possible)

    def refresh(self):

        linux = platform.system() == "Linux"
        cpu = platform.machine().lower() in ("x86_64", "amd64")
        steam = self.steam()
        proton = self.proton()

        self.checks["linux"].config(
            text="✓ READY" if linux else "✗ REQUIRED",
            fg=GREEN if linux else RED
        )

        self.checks["cpu"].config(
            text="✓ READY" if cpu else "✗ REQUIRED",
            fg=GREEN if cpu else RED
        )

        self.checks["steam"].config(
            text="✓ DETECTED" if steam else "✗ NOT FOUND",
            fg=GREEN if steam else RED
        )

        self.checks["proton"].config(
            text="✓ DETECTED" if proton else "✗ NOT FOUND",
            fg=GREEN if proton else RED
        )

        if self.installed():

            self.action.config(
                text="UNINSTALL",
                bg=RED,
                activebackground="#ef6666",
                state="normal"
            )

            self.hero.config(
                text="● OSFR IS INSTALLED",
                fg=GREEN
            )

            self.status.config(
                text="OSFR is installed. Uninstall is available."
            )

        else:

            self.action.config(
                text="INSTALL",
                bg=BLUE,
                activebackground=BLUE_HOVER,
                state="normal"
            )

            self.hero.config(
                text="● READY TO INSTALL",
                fg=GREEN
            )

            self.status.config(
                text="Choose an installation location, then install OSFR."
            )

    def browse(self):

        directory = filedialog.askdirectory(
            title="Choose OSFR installation location"
        )

        if directory:
            self.install_dir.set(directory)
            self.refresh()

    def action_clicked(self):

        if self.busy:
            return

        if self.installed():
            self.uninstall()
        else:
            self.install()

    def install(self):

        if platform.system() != "Linux":
            messagebox.showerror(
                "Unsupported System",
                "This installer requires Linux."
            )
            return

        if platform.machine().lower() not in ("x86_64", "amd64"):
            messagebox.showerror(
                "Unsupported CPU",
                "OSFR currently requires an x86_64 processor."
            )
            return

        if not self.steam():
            messagebox.showerror(
                "Steam Not Found",
                "Steam must be installed before installing OSFR."
            )
            return

        if not self.proton():
            messagebox.showerror(
                "Proton Not Found",
                "Install Steam Proton Experimental first."
            )
            return

        destination = Path(self.install_dir.get()).expanduser()

        try:
            destination.mkdir(parents=True, exist_ok=True)
        except Exception as e:
            messagebox.showerror(
                "Installation Location",
                str(e)
            )
            return

        self.busy = True
        self.action.config(state="disabled")
        self.display_progress = 0.0
        self.target_progress = 0.0
        self.last_step = 0
        self.progress["value"] = 0
        self.progress_text.config(text="Starting installation...")
        self.status.config(text="Installing OSFR...")

        threading.Thread(
            target=self.run_install,
            args=(destination,),
            daemon=True
        ).start()

    def run_install(self, destination):

        env = os.environ.copy()
        env["OSFR_INSTALL_DIR"] = str(destination)
        env["OSFR_STATUS_FILE"] = str(destination / "install-status")

        script = Path.home() / "OSFR-Linux-Installer" / "install.sh"

        try:
            process = subprocess.Popen(
                ["bash", str(script)],
                env=env,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL
            )

            self.poll_install_progress(destination, process)

        except Exception as e:
            self.after(
                0,
                lambda: messagebox.showerror(
                    "Installer Error",
                    str(e)
                )
            )

            self.busy = False
            self.after(0, self.refresh)

    def poll_install_progress(self, destination, process):

        status_file = destination / "install-status"

        if status_file.exists():

            try:
                data = status_file.read_text().strip()

                parts = data.split("|", 2)

                if len(parts) == 3:
                    step, message, percent = parts

                    self.after(
                        0,
                        lambda: self.update_progress(
                            int(step),
                            message,
                            float(percent)
                        )
                    )

            except Exception:
                pass

        if process.poll() is None:

            self.after(
                200,
                lambda: self.poll_install_progress(
                    destination,
                    process
                )
            )

            return

        if process.returncode == 0:

            self.after(
                0,
                lambda: self.update_progress(
                    8,
                    "Installation complete",
                    100
                )
            )

            self.after(
                0,
                lambda: messagebox.showinfo(
                    "Installation Complete",
                    "Open Source Free Realms has been installed successfully."
                )
            )

        else:

            self.after(
                0,
                lambda: messagebox.showerror(
                    "Installation Failed",
                    "The installer returned an error."
                )
            )

        self.busy = False
        self.after(0, self.refresh)

    def update_progress(self, step, message, percent=None):

        # The installer reports the exact milestone percentage.
        # The GUI does not invent or animate progress.
        if percent is None:
            percent = {
                1: 10,
                2: 20,
                3: 30,
                4: 40,
                5: 50,
                6: 70,
                7: 90,
                8: 100
            }.get(step, 0)

        self.last_step = step
        self.display_progress = float(percent)

        self.progress["value"] = self.display_progress

        self.status.config(text=message)

        if percent >= 100:
            self.progress_text.config(
                text="Installation complete  •  100%"
            )
        else:
            self.progress_text.config(
                text=f"Step {step} of 8  •  {int(percent)}%"
            )

    def uninstall(self):

        if self.busy:
            return

        if not messagebox.askyesno(
            "Uninstall OSFR",
            "Completely remove Open Source Free Realms?\n\n"
            "This will remove:\n"
            "• OSFR Launcher\n"
            "• ALL servers and downloaded clients\n"
            "• ALL folders under Servers\n"
            "• Proton prefix\n"
            "• OSFR configuration/cache\n"
            "• Launcher shortcuts and icons\n\n"
            "The OSFR Linux Installer itself will NOT be removed.\n\n"
            "Continue?"
        ):
            return

        self.busy = True
        self.action.config(state="disabled")
        self.status.config(text="Uninstalling OSFR...")
        self.progress["value"] = 0
        self.progress_text.config(text="Stopping OSFR...")
        self.update_idletasks()

        # Stop launcher/game processes.
        for process in ("FreeRealms.exe", "OSFRLauncher"):
            try:
                subprocess.run(
                    ["pkill", "-f", process],
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL
                )
            except Exception:
                pass

        home = Path.home()

        # IMPORTANT:
        # The launcher downloads ALL server/client data here.
        targets = [
            home / ".local" / "share" / "OSFRLauncher",
            home / ".local" / "share" / "OSFR-Linux",
            home / ".local" / "opt" / "OSFR-Linux",
            home / ".cache" / "OSFRLauncher",
            home / ".cache" / "OSFR-Linux",
            home / "Desktop" / "OSFR-Linux.desktop",
            home / "Desktop" / "OSFR-Launcher.desktop",
            home / "Desktop" / "FreeRealms.desktop",
            home / ".local" / "share" / "applications" / "OSFR-Linux.desktop",
            home / ".local" / "share" / "applications" / "OSFR-Launcher.desktop",
            home / ".local" / "share" / "applications" / "FreeRealms.desktop",
            home / ".local" / "share" / "icons" / "osfr-launcher.svg",
            home / ".local" / "share" / "icons" / "OSFRLauncher.png",
            home / ".local" / "share" / "icons" / "OSFR-Linux.png",
        ]

        total = len(targets)

        try:
            for index, target in enumerate(targets, 1):

                self.progress_text.config(
                    text=f"Removing OSFR data... {index}/{total}"
                )
                self.update_idletasks()

                try:
                    if target.is_dir() and not target.is_symlink():
                        shutil.rmtree(target)
                    elif target.exists() or target.is_symlink():
                        target.unlink()
                except FileNotFoundError:
                    pass

                self.progress["value"] = (index / total) * 100
                self.update_idletasks()

            self.progress["value"] = 100
            self.progress_text.config(
                text="Uninstallation complete • 100%"
            )
            self.status.config(
                text="OSFR and all server/client data removed."
            )
            self.update_idletasks()

            messagebox.showinfo(
                "Uninstall Complete",
                "Open Source Free Realms has been completely removed.\n\n"
                "All servers, downloaded clients, launcher data, "
                "Proton data, shortcuts, and icons were removed.\n\n"
                "The OSFR Linux Installer was preserved."
            )

        except Exception as e:

            messagebox.showerror(
                "Uninstall Failed",
                str(e)
            )

        finally:
            self.busy = False
            self.refresh()


if __name__ == "__main__":
    Installer().mainloop()
