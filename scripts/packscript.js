const util = require('util')
const glob = require('glob')
const xml = require('xml-js')
const fs = require('fs')
const exec = util.promisify(require('child_process').exec)

function incorporateArg(parameters, args, arg) {
  for (const key of Object.keys(parameters)) {
    const param = parameters[key];
    const prefix = param.pref.find(p => arg.startsWith(p))
    if (prefix) {
      const value = arg.substring(prefix.length)
      if (param.type === Array) {
        const arr = args[key] || []
        arr.push(value)
        args[key] = arr
      } else if (param.type === Object) {
        const obj = args[key] || {}
        const pair = arg.split('=')
        if (pair.length !== 2) {
          throw new Error(`prefix ${prefix} with value ${value} must contain  the '=' symbol`)
        }
        obj[pair[0]] = pair[1]
        args[key] = obj
      } else {
        if (args[key] !== undefined) {
          throw new Error(`prefix ${prefix} was already found for a non-array property`)
        }
        if (param.type === Boolean) {
          args[key] = value != '0' && value.toLowerCase() != 'no' && value.toLowerCase() != 'false'
        } else {
          args[key] = param.type(value)
        }
      }
      return;
    }
  }
  throw new Error(`Unknown prefix ${arg}`)
}

const parameters = {
  projects: { type: Array, pref: ['p:', 'project:'] },
  command: { type: String, pref: ['c:', 'command:'] },
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

const commands = {
  info: (args) => iterateAsync(args.projectFiles, file => {
    console.info('Project: ' + file)
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
  pack: null,
  publish: null,
  'publish-local': null
}

function fromArgs(argv) {
  const args = {}
  argv.forEach(arg => incorporateArg(parameters, args, arg))
  return args
}

function checkArgs(args) {
  if (!args.projects || !args.projects.length) {
    throw new Error(`Projects are required. Use p: or project: to indicate some projects.`)
  }
  const commandKeys = Object.keys(commands)
  if (!args.command || !commandKeys.includes(args.command)) {
    throw new Error(`Command is required. should be one of ${commandKeys}`)
  }
  return findProjects(args.projects)
    .then(files => {
      args.projectFiles = files
      return args
    })
}

function findProjects(projects) {
  return foldAsync(projects, (project, files) => 
    util.promisify(glob)(project === '*' ? '**/*.fsproj' : project)
      .then(matches => [...files, ...matches]), 
    [])
}

function main() {
  checkArgs(fromArgs([ ...process.argv ].slice(2)))
    .then(args => {
      // console.log('Using args', args)
      return commands[args.command](args)
    })
    .catch(err => {
      console.error(err)
      return null
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


