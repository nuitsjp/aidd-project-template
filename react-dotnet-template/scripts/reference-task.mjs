import { npm, run } from '../reference/scripts/lib.mjs';

const [command, ...args] = process.argv.slice(2);
if (!command)
    throw new Error('実行するコマンドを指定してください。');
if (command === 'npm')
    await npm(...args);
else
    await run(command, args);
