module.exports = function(grunt) {

  var path = require('path');
  var tempDir = '../_temp/';

  var nugetDir = '../../Nuget.builds/'
  var sourceDir = '../../breeze.sharp/'
  var msBuild = 'C:/Windows/Microsoft.NET/Framework/v4.0.30319/MSBuild.exe ';
  var msBuildOptions = ' /p:Configuration=Release /verbosity:minimal ';
 
  var versionNum = getBreezeSharpVersion();

  grunt.file.write(tempDir + 'version.txt', 'Version: ' + versionNum);
  grunt.log.writeln('localAppData: ' + process.env.LOCALAPPDATA);
  
  var nugetPackageNames = [
 	   'Breeze.Sharp',
	];
  
  var breezeSharpDlls = [
    'Breeze.Sharp', 
  ];
  
  var tempPaths = [
     'bin','obj', 'packages','*_Resharper*','*.suo'
  ];
	 
  // Project configuration.
  grunt.initConfig({
    pkg: grunt.file.readJSON('package.json'),
    
    
	  msBuild: {
      // build the Breeze.Sharp sln ( which includes all of the other projects) 
      source: {
        // 'src' here just for 'newer' functionality
        src: '../../Breeze.Sharp/**',
        msBuildOptions: msBuildOptions,
        solutionFileNames: ['../../Breeze.Sharp.sln']
      },
    },
    
    clean: {
      options: {
        // uncomment to test
        // 'no-write': true,
        force: true,
      },
      // remove all previously build .nupkgs
      nupkgs: [ nugetDir + '**/*.nupkg']
    },
    
    copy: {
      // copy all nuget packages into the local nuget test dir
      testNupkg: {
        files: [ { 
          expand: true, 
          cwd: nugetDir, 
          src: ['**/*.nupkg' ], 
          flatten: true,
          dest: process.env.LOCALAPPDATA + '/Nuget/Cache' 
        }]
      }, 
    },

    updateFiles: {
      
      // copy breeze.sharp dll files to each of the nuget sources
      nugetLibs: {
        src: [ sourceDir + 'bin/release/Breeze.sharp.dll', sourceDir + 'bin/release/Breeze.sharp.pdb' ],
        destFolders: [ nugetDir]
      }
    },
    
    // build all nuget packages 
    buildNupkg: {
      build: { src: [ nugetDir + '**/Default.nuspec' ] }
    },
    
    // deploy all nuget packages
    deployNupkg: {
      base: { src: [ nugetDir + '**/*.nupkg'] }
    },
    
    // debugging tool
    listFiles: {
      samples: {
        src: [ nugetDir + '**/Default.nuspec']
      }
    },
   
  });

  grunt.loadNpmTasks('grunt-exec');
  grunt.loadNpmTasks('grunt-contrib-copy');
  grunt.loadNpmTasks('grunt-contrib-clean');
  grunt.loadNpmTasks('grunt-contrib-compress');
  grunt.loadNpmTasks('grunt-newer');

  

   
  grunt.registerMultiTask('msBuild', 'Execute MsBuild', function( ) {
    // dynamically build the exec tasks
    grunt.log.writeln('msBuildOptions: ' + this.data.msBuildOptions);
    var that = this;
    
    this.data.solutionFileNames.forEach(function(solutionFileName) {
      execMsBuild(solutionFileName, that.data);
    });
  });  
  
  grunt.registerMultiTask('updateFiles', 'update files to latest version', function() {
    var that = this;
    this.files.forEach(function(fileGroup) {
      fileGroup.src.forEach(function(srcFileName) {
        grunt.log.writeln('Updating from: ' + srcFileName);
        var baseName = path.basename(srcFileName);
        that.data.destFolders.forEach(function(df) {
          grunt.log.writeln('df: ' + df);
          var destPattern = df + '/**/lib/**/' + baseName;
          grunt.log.writeln('dp: ' + destPattern);
          var destFiles = grunt.file.expand(destPattern);
          destFiles.forEach(function(destFileName) {
            grunt.log.writeln('           to: ' + destFileName);
            grunt.file.copy(srcFileName, destFileName);
          });
        });
      });
    });
  });
  
   grunt.registerMultiTask('buildNupkg', 'package nuget files', function() {   
    this.files.forEach(function(fileGroup) {
      fileGroup.src.forEach(function(fileName) {
        packNuget(fileName);
      });
    });
  });
  
  grunt.registerMultiTask('deployNupkg', 'deploy nuget package', function() {   
    this.files.forEach(function(fileGroup) {
      fileGroup.src.forEach(function(fileName) {
        grunt.log.writeln('Deploy: ' + fileName);
        var folderName = path.dirname(fileName);
        runExec('deployNupkg', {
          cmd: 'nuget push ' + fileName 
        });
      });
    });
  });
  
  // for debugging file patterns
  grunt.registerMultiTask('listFiles', 'List files', function() {
    grunt.log.writeln('target: ' + this.target);
    
    this.files.forEach(function(fileGroup) {
      fileGroup.src.forEach(function(fileName) {
        grunt.log.writeln('file: ' + fileName);
      });
    });
  });

  grunt.registerTask('build', 
   ['newer:msBuild:source' ]);
  grunt.registerTask('packageNuget',   
   [ 'clean:nupkgs', 'newer:updateFiles', 'buildNupkg', 'copy:testNupkg']);
  
  grunt.registerTask('default', ['build', 'packageNuget']);
  
  // ------------------- local functions ----------------------
    
  function getBreezeSharpVersion() {
     var versionFile = grunt.file.read( sourceDir + '/properties/assemblyInfo.cs');    
     var regex = /\s+AssemblyVersion\("(.*)"/
     var matches = regex.exec(versionFile);
     
     if (matches == null) {
        throw new Error('Version number not found');
     }
     // matches[0] is entire version string - [1] is just the capturing group.
     var versionNum = matches[1];
     grunt.log.writeln('version: ' + versionNum);
     return versionNum;
  }
  
  function join(a1, a2) {
    var result = [];
    a1.forEach(function(a1Item) {
      a2.forEach(function(a2Item) {
        result.push(a1Item + '**/' + a2Item);
      });
    });
    return result;
  }
  
  function packNuget(nuspecFileName) {
    var folderName = path.dirname(nuspecFileName);
    grunt.log.writeln('Nuspec folder: ' + folderName);
    
    var text = grunt.file.read(nuspecFileName);
    var folders = folderName.split('/');
    var folderId = folders[folders.length-1];
    
    text = text.replace(/{{version}}/g, versionNum);
    text = text.replace(/{{id}}/g, folderId);
    var destFileName = folderName + '/' + folderId + '.nuspec';
    grunt.log.writeln('nuspec file: ' + destFileName);
    grunt.file.write(destFileName, text);
    // 'nuget pack $folderName.nuspec'
    runExec('nugetpack', {
      cwd: folderName,
      cmd: 'nuget pack ' + folderId + '.nuspec'
    });   

  }

  function execMsBuild(solutionFileName, config ) {
    grunt.log.writeln('Executing solution build for: ' + solutionFileName);
    
    var cwd = path.dirname(solutionFileName);
    var baseName = path.basename(solutionFileName);
    var rootCmd = msBuild + '"' + baseName +'"' + config.msBuildOptions + ' /t:' 
    
    runExec('msBuildClean', {
      cwd: cwd,
      cmd: rootCmd + 'Clean'
    });
    runExec('msBuildRebuild', {
      cwd: cwd,
      cmd: rootCmd + 'Rebuild'
    });

  }
  
  var index = 0;
  
  function runExec(name, config) {
    var name = name+'-'+index++;
    grunt.config('exec.' + name, config);
    grunt.task.run('exec:' + name);
  }
  
  function log(err, stdout, stderr, cb) {
    if (err) {
      grunt.log.write(err);
      grunt.log.write(stderr);
      throw new Error('Failed');
    }

    grunt.log.write(stdout);

    cb();
  }


};