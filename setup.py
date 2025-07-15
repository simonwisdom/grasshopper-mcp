from setuptools import setup, find_packages
import os

# Read README.md as long description
with open("README.md", "r", encoding="utf-8") as fh:
    long_description = fh.read()

setup(
    name="grasshopper-mcp",
    version="0.1.0",
    packages=find_packages(),
    include_package_data=True,
    install_requires=[
        "mcp>=0.1.0",
        "websockets>=10.0",
        "aiohttp>=3.8.0",
    ],
    entry_points={
        "console_scripts": [
            "grasshopper-mcp=grasshopper_mcp.bridge:main",
        ],
    },
    author="Simon Wisdom",
    author_email="simonwisdom@duck.com",
    description="Grasshopper MCP Bridge Server",
    long_description=long_description,
    long_description_content_type="text/markdown",
    keywords="grasshopper, mcp, bridge, server",
    url="https://github.com/simonwisdom/grasshopper-mcp",
    classifiers=[
        "Development Status :: 3 - Alpha",
        "Intended Audience :: Developers",
        "Programming Language :: Python :: 3",
        "Programming Language :: Python :: 3.8",
        "Programming Language :: Python :: 3.9",
    ],
    python_requires=">=3.8",
)
