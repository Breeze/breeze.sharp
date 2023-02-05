var fs   = require('fs-extra');
var path = require('path');
var child_process = require('child_process');

var _sourceDir = "../Breeze.Sharp/";
var _nugetDir = '../Nuget.builds/'
var _msBuildCmd = '"C:/Program Files/Microsoft Visual Studio/2022/Enterprise/MSBuild/Current/Bin/MSBuild.exe" '

// var _msBuildOptions = ' /p:Configuration=Release /verbosity:minimal ';
var _msBuildOptions = ' /p:Configuration=Release /verbosity:minimal  /clp:NoSummary;NoItemAndPropertyList;ErrorsOnly';
var _solutionFileName = '../Breeze.Sharp.6.sln';

var _versionNum = getBreezeSharpVersion();
// flow is msBuildSolution -> copyFiles -> packNuget -> deployNugetLocal
msBuildSolution();

function msBuildSolution() {
  if (!fs.existsSync(_solutionFileName)) {
    throw new Error(_solutionFileName + ' does not exist');
  }
  var baseName = path.basename(_solutionFileName);
  var rootCmd = _msBuildCmd + '"' + baseName +'"' + _msBuildOptions + ' /t:'

  var cwd = path.dirname(_solutionFileName);
  var options = { cwd: cwd };
  execCommands([rootCmd + 'Clean', rootCmd + 'Rebuild'], options, function() {
    copyFiles();
    packNuget(_nugetDir + "Breeze.Sharp/Default.6.nuspec", function () {
      deployNugetLocal(_nugetDir + "Breeze.Sharp");
    });
  });
}

function copyFiles() {
  var lib = _nugetDir + "Breeze.Sharp/lib";
  // fs.removeSync(lib);
  if (!fs.existsSync(lib)) {
    fs.mkdirSync(lib);
    fs.mkdirSync(lib + "/net6.0");
  }
  var src = _sourceDir + "bin/Release/net6.0/";
  var dest = _nugetDir + "Breeze.Sharp/lib/net6.0/";
  var files = ["Breeze.Sharp.dll", "Breeze.Sharp.pdb", "Breeze.Sharp.xml"];
  files.forEach(name => {
    fs.copyFileSync(src + name, dest + name);
  });
}

function packNuget(nuspecFileName, callback) {
  var folderName = path.dirname(nuspecFileName);
  var text = fs.readFileSync(nuspecFileName, { encoding: 'utf8'});
  var folders = folderName.split('/');
  var folderId = folders[folders.length-1];

  text = text.replace(/{{version}}/g, _versionNum);
  text = text.replace(/{{id}}/g, folderId);

  var destFileName = folderId + '.nuspec';
  console.log('Packing nuspec file: ' + destFileName + ' in folder ' + folderName);
  fs.writeFileSync(destFileName, text);
  var cmd = 'nuget pack ' + destFileName;

  execCommands([cmd], { cwd: folderName }, callback);
}

function deployNugetLocal(startPath) {
  var nugetDir = process.env.LOCALAPPDATA + '\\Nuget\\Test';
  var cmds = [];
  var files=fs.readdirSync(startPath);
  files.forEach(f => {
    if (f.endsWith(".nupkg")) {
      var s = path.join(startPath, f);
      cmds.push("nuget add " + s + " -Source " + nugetDir);
    }
  });
  if (cmds.length) {
    execCommands(cmds, {}, null);
  }
}

function execCommands(cmds, options, callback) {
  console.log(cmds[0]);
  child_process.exec(cmds[0], options, (error, stdout, stderr) => {
    if (stdout) {
      console.log(stdout);
    }
    if (error) {
      console.log(error);
      console.log(stderr);
    } else {
      cmds = cmds.slice(1);
      if (cmds.length) {
        execCommands(cmds, options, callback);
      } else if (callback) {
        callback();
      } else {
        console.log("done.");
      }
    }
  });
}

function getBreezeSharpVersion() {
     var versionFile = fs.readFileSync( _sourceDir + 'Breeze.Sharp.6.csproj', { encoding: 'utf8'});
     var regex = /\s+<Version>(.*)<\/Version>/
     var matches = regex.exec(versionFile);

     if (matches == null) {
        throw new Error('Version number not found');
     }
     // matches[0] is entire version string - [1] is just the capturing group.
     var versionNum = matches[1];
     console.log('version: ' + versionNum);
     return versionNum;
  }
