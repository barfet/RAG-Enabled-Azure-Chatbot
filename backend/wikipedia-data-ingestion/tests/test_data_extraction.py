import os
import pytest
from unittest.mock import patch, MagicMock
import sys
import json
from pathlib import Path

# Add the src directory to the path so we can import our modules
sys.path.append(str(Path(__file__).parent.parent / "src"))

# Mock load_dotenv to avoid loading actual .env file during tests
with patch('dotenv.load_dotenv'):
    from data_extraction import WikipediaDataExtractor

class TestWikipediaDataExtraction:
    """
    Test suite for the Wikipedia data extraction functionality.
    """
    
    @pytest.fixture
    def mock_dataset(self):
        """Fixture that returns a mock HuggingFace dataset."""
        mock_dataset = MagicMock()
        sample_data = [
            {
                "id": "12345",
                "url": "https://en.wikipedia.org/wiki/Sample_Article_1",
                "title": "Sample Article 1",
                "text": "This is the content of sample article 1. It contains some information about a topic.",
            },
            {
                "id": "67890",
                "url": "https://en.wikipedia.org/wiki/Sample_Article_2",
                "title": "Sample Article 2",
                "text": "This is the content of sample article 2. It also contains some information about a different topic.",
            }
        ]
        # Configure the mock to return sample data when sliced
        mock_dataset.__getitem__.return_value = sample_data[0]
        mock_dataset.select.return_value = MagicMock()
        mock_dataset.select.return_value.__iter__.return_value = iter(sample_data)
        mock_dataset.select.return_value.__len__.return_value = len(sample_data)
        return mock_dataset

    @pytest.fixture
    def extractor(self):
        """Fixture that returns a WikipediaDataExtractor instance with mock config."""
        with patch.dict(os.environ, {
            "WIKIPEDIA_DATASET_NAME": "wikimedia/wikipedia",
            "WIKIPEDIA_SUBSET": "20220301.en",
            "WIKIPEDIA_SAMPLE_SIZE": "10"
        }):
            return WikipediaDataExtractor()
    
    @patch("data_extraction.load_dataset")
    def test_load_wikipedia_subset(self, mock_load_dataset, extractor, mock_dataset):
        """Test loading a subset of Wikipedia articles."""
        mock_load_dataset.return_value = mock_dataset
        
        # Execute the method under test
        articles = extractor.load_wikipedia_subset(sample_size=2)
        
        # Assertions
        mock_load_dataset.assert_called_once_with(
            "wikimedia/wikipedia", "20220301.en", split="train"
        )
        assert len(articles) == 2
        assert articles[0]["title"] == "Sample Article 1"
        assert articles[1]["title"] == "Sample Article 2"
        
    @patch("data_extraction.load_dataset")
    def test_extract_metadata(self, mock_load_dataset, extractor, mock_dataset):
        """Test extracting metadata from Wikipedia articles."""
        mock_load_dataset.return_value = mock_dataset
        
        # Execute the method under test
        articles = extractor.load_wikipedia_subset(sample_size=1)
        metadata = extractor.extract_metadata(articles[0])
        
        # Assertions
        assert metadata["id"] == "12345"
        assert metadata["title"] == "Sample Article 1"
        assert metadata["url"] == "https://en.wikipedia.org/wiki/Sample_Article_1"
        assert "last_updated" in metadata  # Should add a timestamp
        
    @patch("data_extraction.BlobServiceClient")
    @patch("data_extraction.load_dataset")
    def test_save_to_blob_storage(self, mock_load_dataset, mock_blob_service, extractor, mock_dataset):
        """Test saving articles to Azure Blob Storage."""
        mock_load_dataset.return_value = mock_dataset
        mock_blob_client = MagicMock()
        mock_blob_service.from_connection_string.return_value.get_container_client.return_value.get_blob_client.return_value = mock_blob_client
        
        # Execute the methods under test
        articles = extractor.load_wikipedia_subset(sample_size=1)
        success = extractor.save_to_blob_storage(articles[0], "container-name")
        
        # Assertions
        assert success is True
        mock_blob_client.upload_blob.assert_called_once()
        # Check that the data sent to upload_blob is valid JSON
        call_args = mock_blob_client.upload_blob.call_args
        uploaded_data = call_args[1]["data"]
        # Try to parse it as JSON to ensure it's valid
        parsed = json.loads(uploaded_data)
        assert parsed["title"] == "Sample Article 1"
