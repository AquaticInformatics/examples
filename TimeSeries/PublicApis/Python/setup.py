from setuptools import setup

setup(
    name="aquarius-timeseries-client",
    py_modules=["timeseries_client"],
    version="0.1",
    description="Python client for Aquarius TimeSeries API",
    long_description=open("README.md").read(),
    long_description_content_type="text/markdown",
    url="https://github.com/AquaticInformatics/Examples",
    install_requires=(
        "requests",
        "pyrfc3339"
    )
)
