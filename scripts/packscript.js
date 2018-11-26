const util = require('util')
const glob = require('glob')
const xml = require('xml-js')
const fs = require('fs')
const path = require('path')
const rm = util.promisify(require('rimraf'))
const exec = util.promisify(require('child_process').exec)

const buildDir = 'build'

function PackError(code, message) {
  Error.captureStackTrace(this, this.constructor)
  this.name = this.constructor.name
  this.code = code
  this.message = message
}
util.inherits(PackError, Error)

const parameters = {
  projects: { type: Array, pref: ['p:', 'project:'] },
  command: { type: String, pref: ['c:', 'command:'] },
  showArgs: { type: Boolean, pref: ['-a', '--show-arguments'], default: false },
  clean: { type: Boolean, pref: ['-c', '--clean'], default: true },
  forceBuild: { type: Boolean, pref: ['-f', '--force-build'], default: true },
  buildDir: { type: String, pref: ['o:', 'output:'], default: buildDir },
  nugetSource: { type: String, pref: ['s:', 'source:'] },
}

function incorporateArg(parameters, args, arg) {
  function setKey(key, valueFn) {
    try {
      return { ...args, [key]: valueFn() }
    } catch (err) {
      if (err instanceof PackError) {
        throw err
      } else {
        throw new PackError('ArgError', `Error parsing argument ${key}: ${err.message}`)
      }
    }
  }
  for (const key of Object.keys(parameters)) {
    const param = parameters[key];
    const prefix = param.pref.find(p => arg.startsWith(p))
    if (prefix) {
      const value = arg.substring(prefix.length)
      if (param.type === Array) {
        return setKey(key, () => ([...(args[key] || []), value]))
      } else if (param.type === Object) {
        const pair = arg.split('=')
        if (pair.length !== 2) {
          throw new PackError('ArgError', `prefix ${prefix} with value ${value} must contain  the '=' symbol`)
        }
        return setKey(key, () => ({ ...(args[key] || {}), [pair[0]]: pair[1] }))
      } else {
        if (args[key] !== undefined) {
          throw new PackError('ArgError', `prefix ${prefix} was already found for a non-array property`)
        }
        if (param.type === Boolean) {
          return setKey(key, () => (value != '0' && value.toLowerCase() != 'no' && value.toLowerCase() != 'false'))
        } else {
          return setKey(key, () => param.type(value))
        }
      }
      return;
    }
  }
  throw new PackError('ArgError', `Unknown prefix ${arg}`)
}

function fromArgs(argv) {
  const defaults = Object.keys(parameters)
    .filter(key => parameters[key].default !== undefined)
    .reduce((a, k) => ({ ...a, [k]: parameters[k].default }), {})
  const args =  argv.reduce((a, arg) => incorporateArg(parameters, a, arg), {})
  return { ...defaults, ...args }
}

function foldAsync(arr, fn, initState) {
  return new Promise((r, x) => {
    function loop(index, state) {
      if (index < arr.length) {
        try {
          fn(arr[index], state)
            .then(newState => loop(index + 1, newState))
            .catch(x)
        } catch (error) {
          x(error)
        }
      } else {
        r(state)
      }
    }
    loop(0, initState)
  })
}

function iterateAsync(arr, fn) {
  return foldAsync(arr, fn, null)
}

function collectAsync(arr, fn) {
  return foldAsync(arr, (s, item) => {
    return fn(item)
      .then(v => [...s, item])
      .catch(err => Promise.resolve(s))
  }, [])
}

function xpath(elem, ...steps) {
  return steps.reduce((e, s) => {
    if (!e) {
      return null
    } else if (typeof s === 'number') {
      return e.elements && e.elements[s]
    } else if (typeof s === 'string') {
      if (s.startsWith('@')) {
        return e.attributes && e.attributes[s.substring(1)]
      } else if (s === 'text()') {
        return e.elements && e.elements.filter(x => x.type === 'text').map(x => x.text)[0]
      } else {
        return e.elements && e.elements.find(x => x.type === 'element' && x.name === s)
      }
    } else if (typeof s === 'function') {
      return s(e)
    } else {
      return null;
    }
  }, elem)
}

function getProjectProps(file) {
  return util.promisify(fs.readFile)(file, 'utf-8')
  .then(text => xml.xml2js(text))
  .then(json => xpath(json, 0, 'PropertyGroup'))
  .then(props => {
    if (props) {
      return {
        PackageId: xpath(props, 'PackageId', 'text()'),
        Version: xpath(props, 'Version', 'text()'),
        Authors: xpath(props, 'Authors', 'text()'),
        Company: xpath(props, 'Company', 'text()'),
        Product: xpath(props, 'Product', 'text()'),
      }
    } else {
      return null
    }
  })
}

function cleanDir(args, dir) {
  if (args.clean) {
    console.info(`    Cleaning directory: ${dir}`)
    return rm(dir)
  } else {
    return Promise.resolve()
  }
}

function packProject(file) {
  const dir = path.join(process.cwd(), buildDir)
  const command = `msbuild ${file} /t:pack /p:Configuration=Release /p:OutputPath=${dir}`
  console.info(`    shell: ${command}`)
  return exec(command)
    .then(() => getProjectProps(file))
    .then(props => {
      return `${props.PackageId}.${props.Version}.nupkg`
    })
}

function packProjects(args) {
  return cleanDir(args, args.buildDir)
  .then(() =>
    iterateAsync(args.projectFiles, file => {
      console.info('Pack project: ' + file)
      return packProject(file)
        .then(dest => {
          console.error(`    Success: ${dest}`)
        })
        .catch(err => {
          console.error(`    !!! Error packing project ${file}: ${err.message}`)
        })
    }))
}

function publishPackage(nupkg, source) {
  const sourceCmd = source ? ` -Source ${source}` : ''
  const command = `nuget push ${nupkg}${sourceCmd}`
  console.info(`    shell: ${command}`)
  return exec(command)
}

function publishPackageForProject(args, file) {
  const dir = path.join(process.cwd(), args.buildDir)
  return getProjectProps(file)
    .then(props => `${props.PackageId}.${props.Version}.nupkg`)
    .then(fileName => path.join(dir, fileName))
    .then(nupkg => publishPackage(nupkg, args.source))
}

function publishProjects(args) {
  return iterateAsync(args.projectFiles, file => {
    console.info('Publish project: ' + file)
    return publishPackageForProject(args, file)
      .catch(err => {
        console.error(`    !!! Error publishing project ${file}: ${err.message}`)
      })
  })
}

function publishCommand(args, defaultSource) {
  if (defaultSource)
    args.source = args.source || defaultSource
  const prom = args.forceBuild ? packProjects(args) : Promise.resolve()
  return prom.then(() => publishProjects(args))
}

const commands = {
  info: (args) => iterateAsync(args.projectFiles, file => {
    console.info('Project info: ' + file)
    return getProjectProps(file)
      .then(props => {
        if (!props) {
          console.warn('    This project is not properly configured')
        } else {
          console.info('    PackageId: ', props.PackageId)
          console.info('    Version  : ', props.Version)
          console.info('    Authors  : ', props.Authors)
          console.info('    Company  : ', props.Company)
          console.info('    Product  : ', props.Product)
        }
        console.info()
      })
  }),
  pack: packProjects,
  publish: (args) => publishCommand(args),
  'publish-local': (args) => publishCommand(args, 'Locals')
}

function checkArgs(args) {
  if (!args.projects || !args.projects.length) {
    throw new PackError('ArgError', `Projects are required. Use p: or project: to indicate some projects.`)
  }
  const commandKeys = Object.keys(commands)
  if (!args.command || !commandKeys.includes(args.command)) {
    throw new PackError('ArgError', `Command is required. should be one of ${commandKeys}`)
  }
  return findProjects(args.projects)
    .then(files => {
      args.projectFiles = files.sort((a, b) => path.basename(a).localeCompare(path.basename(b)))
      if (!args.projectFiles.length) {
        throw new PackError('Projects', `No projects were found`)
      }
      return args
    })
}

const commonProjects = require('./common-projects.json')

function findProjects(projects) {
  return foldAsync(projects, (project, files) => {
    return util.promisify(glob)(commonProjects[project] || project)
      .then(matches => [...files, ...matches])
  }, [])
}

function main() {
  checkArgs(fromArgs([ ...process.argv ].slice(2)))
    .then(args => {
      if (args.showArgs) {
        delete args.showArgs
        console.log('Using args: ', args)
      }
      return args
    })
    .then(args => commands[args.command](args))
    .catch(err => {
      if (err instanceof PackError) {
        console.error(`${err.code}: ${err.message}`)
      } else {
        console.error(err)
      }
      return true
    })
}

main()

// exec(`msbuild .\\SharpFunky\\SharpFunky.fsproj /t:pack /p:Configuration=Release`)
//   .then(data => {
//     console.log('Success with ', data)
//   })
//   .catch(data => {
//     console.log('Failed with ', data)
//   })
