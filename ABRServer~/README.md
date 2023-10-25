# ABR Server

This folder contains the code for the ABR server and the ABR Compose Design Interface.


## IN PROGRESS TODOS


### General UI/Server related Todos

- [ ] Support changing background color in browser editor
- [ ] Support changing lighting in browser editor
- [ ] Support creating/editing Data Impression Groups in browser editor

### Library Server Rewrite

Major additions in progress (adding library and applets here...)

The goal is to significantly simplify the approach to managing libraries for
ABR. Right now there's the cloud library at
<https://sculptingvis.tacc.utexas.edu>. Artists can also have their own
individual libraries via the [Sculpting Vis
Library](https://hub.docker.com/r/bridgerherman/sculpting-vis-library) docker
image (this is a clone of the code running on
<https://sculptingvis.tacc.utexas.edu>).

This code started out as an experiment under a VIS deadline, and is
(unsurprisingly) a bit more complicated than it needs to be. So, the aim of this
progress is to simplify the library management and applet code, and add it to
the main ABR compose interface so *all ABR components are located in one place*.

The major TODOs are:

- [x] Analyze current server VisAsset management strategy in this repo. the
following are already implemented:
    - [x] Save from local VisAsset
    - [x] Download VisAsset from other library
    - [x] Remove VisAsset
    - [ ] In-Memory VisAsset Cache (currently views.py, move this to library manager)
- [ ] Create VisAsset artifact.json schema in ABRSchemas~
    - [x] Port basic fields from current `artifact.json`
    - [ ] Design workflow from library to compose (likely don't want *everything* that's in library to be in the palette... by default, put it in the local library, but only show certain ones?)
- [ ] Create new Django sub-apps named /library, /applets
- [ ] Write Django infrastructure:
    - [ ] Create/Read/Delete VisAssets
    - [ ] Create/Read/Delete Gradients
- [ ] Migrate applets to this repo
    - [ ] Color Loom
    - [ ] Infinite Line
    - [ ] Texture Mapper
        - [ ] Change over code from WASM to vanilla JavaScript for readability?
    - [ ] Glyph Aligner
        - [ ] Clarify directions
        - [ ] Use `bpy` Blender python module as a dependency of ABR server

## Installation

To get started with development, first make sure you have the `pipenv` package.
This enables all developers of the server to share the same Python configuration
for this app. Essentially, the Pipenv contains a "local" copy of each dependency
that's unique to this project to reduce the chance of conflicting dependencies
with your system Python installation. Check out the [pipenv
project](https://docs.pipenv.org/) for more information. If you're on Windows,
replace `python3` with `py`.

All these commands are tested with Python 3.10; they are NOT guaranteed to work
with other versions of Python.

```
python3 pip install --user pipenv
```

Then, install the local dependencies:

```
python3 -m pipenv install
```

The first time you run this command, you may need to provide Python path:

```
python3 -m pipenv --python=/c/Python311/python.exe install
```

Then, to begin development, "activate" the Pipenv by entering a shell:

```
python3 -m pipenv shell
```

From here, you should have access to all the dependencies of the project.


## Running the server

The server can be run local-only (on localhost:8000 by default):

```
python manage.py runserver
```

The server can also be broadcast to other devices:

```
python manage.py runserver 0.0.0.0:8000
```


To enable live-reloading (automatically refresh browser when a file is
changed), run these commands in separate terminals (the settings_dev above
enables live-reloading to work):

```
python manage.py livereload
```

```
python manage.py runserver --settings=abr_server.settings_dev
```



## Building the executable version

The ABR server can also be built to an executable for easy distribution.

First, you need the `pyinstaller` package. If you've followed the steps above
with installing and activating the pipenv shell, this should already be taken care of. You can check by running:

```
pyinstaller --version
```

If for some reason that doesn't work, you can run:

```
python3 -m pip install pyinstaller
```

Restart your terminal to make sure `pyinstaller` ends up on your PATH.


Then, to build the executable, run one of the following commands depending on
your OS (add more here for OS's that aren't supported yet):

Windows x64:

```
pyinstaller  --name="ABRServer-Windows-X64" --hidden-import="compose" --hidden-import="compose.urls" --hidden-import="api" --hidden-import="api.urls" --hidden-import="abr_server.routing" --hidden-import="_socket" --add-data="templates:templates" --add-data="static:static" --add-data="abr_server.cfg:." manage.py
```

Mac x64 (Intel):

```
pyinstaller  --name="ABRServer-OSX-X64" --hidden-import="compose" --hidden-import="compose.urls" --hidden-import="api" --hidden-import="api.urls" --hidden-import="abr_server.routing" --hidden-import="_socket" --add-data="templates:templates" --add-data="static:static" --add-data="abr_server.cfg:." manage.py
```

Mac ARM (M1 or M2):

```
pyinstaller  --name="ABRServer-OSX-ARM64" --hidden-import="compose" --hidden-import="compose.urls" --hidden-import="api" --hidden-import="api.urls" --hidden-import="abr_server.routing" --hidden-import="_socket" --add-data="templates:templates" --add-data="static:static" --add-data="abr_server.cfg:." manage.py
```

this will output an executable (for the OS/architecture that you run
pyinstaller on) to the folder `./dist/ABRServer`. You can zip this up, etc.
for distribution. In Unity, the ABRServer script will also look in these
folders depending on what OS you're on.
