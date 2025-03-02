"""
Module for extracting Wikipedia data from the Hugging Face datasets library.
"""
import os
import json
import logging
import datetime
from typing import Dict, List, Any, Optional
from datasets import load_dataset
from azure.storage.blob import BlobServiceClient
from dotenv import load_dotenv

# Load environment variables from .env file
load_dotenv()

class WikipediaDataExtractor:
    """
    Class for extracting Wikipedia data from Hugging Face datasets.
    """
    def __init__(self):
        """
        Initialize the WikipediaDataExtractor with configuration from environment variables.
        """
        self.dataset_name = os.environ.get("WIKIPEDIA_DATASET_NAME", "wikimedia/wikipedia")
        self.subset = os.environ.get("WIKIPEDIA_SUBSET", "20220301.en")
        self.sample_size = int(os.environ.get("WIKIPEDIA_SAMPLE_SIZE", "1000"))
        self.logger = logging.getLogger(__name__)
    
    def load_wikipedia_subset(self, sample_size: Optional[int] = None) -> List[Dict[str, Any]]:
        """
        Load a subset of Wikipedia articles from the Hugging Face dataset.
        
        Args:
            sample_size: Number of articles to load. Defaults to the value in env var.
            
        Returns:
            List of article dictionaries.
        """
        if sample_size is None:
            sample_size = self.sample_size
            
        try:
            self.logger.info(f"Loading Wikipedia dataset {self.dataset_name}/{self.subset}")
            dataset = load_dataset(self.dataset_name, self.subset, split="train")
            
            self.logger.info(f"Selecting {sample_size} articles from dataset")
            # Select a random sample of specified size
            sampled_dataset = dataset.select(range(sample_size))
            
            # Convert to list of dictionaries
            articles = list(sampled_dataset)
            self.logger.info(f"Successfully loaded {len(articles)} articles")
            return articles
            
        except Exception as e:
            self.logger.error(f"Error loading Wikipedia dataset: {str(e)}")
            raise
    
    def extract_metadata(self, article: Dict[str, Any]) -> Dict[str, Any]:
        """
        Extract and format metadata from a Wikipedia article.
        
        Args:
            article: Wikipedia article dictionary
            
        Returns:
            Dictionary of metadata fields
        """
        try:
            metadata = {
                "id": article.get("id", ""),
                "title": article.get("title", ""),
                "url": article.get("url", ""),
                "last_updated": datetime.datetime.utcnow().isoformat(),
            }
            
            # Extract categories if available
            if "categories" in article:
                metadata["categories"] = article["categories"]
                
            return metadata
        except Exception as e:
            self.logger.error(f"Error extracting metadata: {str(e)}")
            raise
    
    def save_to_blob_storage(self, article: Dict[str, Any], container_name: Optional[str] = None) -> bool:
        """
        Save a Wikipedia article to Azure Blob Storage.
        
        Args:
            article: Wikipedia article dictionary
            container_name: Name of the container to save to. Defaults to env var.
            
        Returns:
            True if successful, False otherwise
        """
        if container_name is None:
            container_name = os.environ.get("AZURE_STORAGE_CONTAINER_NAME", "wikipedia-raw")
            
        connection_string = os.environ.get("AZURE_STORAGE_CONNECTION_STRING")
        if not connection_string:
            self.logger.error("Missing Azure Storage connection string")
            return False
            
        try:
            # Create a blob service client
            blob_service_client = BlobServiceClient.from_connection_string(connection_string)
            container_client = blob_service_client.get_container_client(container_name)
            
            # Create container if it doesn't exist
            if not container_client.exists():
                self.logger.info(f"Creating container {container_name}")
                container_client.create_container()
            
            # Generate a unique blob name based on article ID or title
            blob_name = f"{article.get('id', 'unknown')}-{article.get('title', 'untitled').replace(' ', '_')}.json"
            
            # Get a blob client
            blob_client = container_client.get_blob_client(blob_name)
            
            # Convert article to JSON
            article_json = json.dumps(article, ensure_ascii=False)
            
            # Upload data
            blob_client.upload_blob(data=article_json, overwrite=True)
            self.logger.info(f"Successfully uploaded article to {blob_name}")
            
            return True
        except Exception as e:
            self.logger.error(f"Error saving to Blob Storage: {str(e)}")
            return False
