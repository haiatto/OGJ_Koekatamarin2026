"""Fetch the official pinned ReazonSpeech k2-v2 CPU model for Quest."""
from pathlib import Path
import shutil
from setup_speech import fetch, CACHE, ASSETS

REVISION = '291488c8151be24d7da4bf7af26e533fad96e407'
BASE = 'https://huggingface.co/reazon-research/reazonspeech-k2-v2/resolve/' + REVISION + '/'
FILES = {
    'encoder-epoch-99-avg-1.int8.onnx': '2c7bd08a8a99f9ddd0d9e458456577b1f6279214e51426f114f9eced44c54e1d',
    'decoder-epoch-99-avg-1.onnx': '58b18211ae06265466bfa17172dab574df94f76c8bcb61a3640c28ba860e4124',
    'joiner-epoch-99-avg-1.int8.onnx': '49cc7ea1d3d35a40a27442db5e89996da64bf0e683a903dce76e99e57a12e4de',
    'tokens.txt': '2c3ac659818a48a0c04010e0593bbc4d7c8a24a054340b01131499c05fd52def',
}

def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    target = ASSETS / 'StreamingAssets/Speech/Reazon'
    target.mkdir(parents=True, exist_ok=True)
    for name, digest in FILES.items():
        source = fetch('reazon-' + name, BASE + name, digest)
        shutil.copyfile(source, target / name)
    print('ReazonSpeech ready; no model download is needed on Quest.', flush=True)

if __name__ == '__main__':
    main()
