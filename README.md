# SocrataCache

SocrataCache aims to provide a caching/downloading layer for Socrata Open Data services.
SocrataCache does this by allowing you to define the datasets you want to download once they come available, and optionally provide the columns you want to download as well.
In this way you can automatically download all data you need automatically and re-distribute it to your own services.

## Story behind

I personally consume a lot of [open data](https://opendata.rdw.nl/) from the Dutch vehicle authority (RDW). 
If you're a bit enthusiastic with your projects, the data you're pulling in will quickly build up to a pile of 20+ Gigabyte. 
Since I consume the same data in multiple project, I was looking for a solution that did allow me to not be dependent on a slow download of multiple big uncompressed files. 
SocrataCache makes sure files are downloaded automatically which enables me to re-distribute the compressed files to my own services. In my case compression resulted in a 80% file size reduction!

This piece of software is far from perfect, since it is my first serious greenfield project in .NET. Please feel invited to provide feedback and/or suggestions as you please.


