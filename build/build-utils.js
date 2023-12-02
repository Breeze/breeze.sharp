var fs = require("fs-extra");
var exec = require('child_process').exec;
var del = require('del');


// Get single command line argument, and show usage method if its incorrect */
function getArg() {
  var args = process.argv.slice(2);
  if (args.length !== 1 || args[0].indexOf('.js') >= 0) {
    var msg = "Usage: " + process.argv[0] + " " + process.argv[1] + " [filenameRoot]"
    throw new Error(msg);
  }
  return args[0];
}

// exec cmd, then call fn if cmd was successful 
// options.cwd = current working dir
// cb is function(err, stdout, stderr);
function execCmd(cmd, options, callback) {
  options = options || {};
  exec(cmd, function (error, stdout, stderr) {
    stdout && console.log('stdout: ' + stdout);
    stderr && console.log('stderr: ' + stderr);
    error && console.log('error: ' + error);
    if (callback) callback(error, stdout, stderr);
  });
}

/** Return the first line of text from the file */
function readFirstLine(filename) {
  const data = fs.readFileSync(filename, {encoding:'utf8', flag:'r'});
  const line = data.toString().split(/[\r\n]/)[0];
  return line;
}

/** Replace strings in a file. 
 * @param filename input file name
 * @param string or array of strings to search
 * @param string or array of strings to replace
 * @param outfile output file name.  If not provided, input file is overwritten.
*/
function replaceInFile(filename, search, replace, outfile) {
  fs.readFile(filename, 'utf8', function (err,data) {
      if (err) {
          return console.log(err);
      }
      var result = replaceInString(data, search, replace);
      if (result === data) {
          console.log("replaceInFile: no change to file " + filename);
          return;
      }
      outfile = outfile || filename;
      fs.writeFile(outfile, result, 'utf8', function (err) {
          if (err) return console.log(err);
      });
      console.log("replaceInFile: changed file " + outfile);
  });
}

/** Replace strings in the data. Search and replace args may be arrays */
function replaceInString(data, search, replace) {
  if (Array.isArray(search)) {
    for (var i=0; i<search.length; i++) {
      data = data.replaceAll(search[i], replace[i]);
    }
    return data;
  } else {
    return data.replaceAll(search, replace);
  }
}


module.exports = {
  execCmd: execCmd,
  getArg: getArg,
  readFirstLine: readFirstLine,
  replaceInFile: replaceInFile
}
